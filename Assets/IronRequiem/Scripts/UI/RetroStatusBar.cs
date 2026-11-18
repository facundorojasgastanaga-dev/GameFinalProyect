using TMPro;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace IronRequiem
{
    /// <summary>
    /// Gameplay HUD frame (16/11 column layout); reads the existing health, inventory, melee and module owners.
    /// Left column ARMAS (current weapon, 9 slots, energy/heat, OVERDRIVE), right column ESTADO DEL CUERPO (health,
    /// body rows derived from real state, fist charge) and the UNIDAD 01 avatar box at the bottom centre.
    /// The hierarchy is built at runtime in Awake; the scene only stores this component and its references.
    /// </summary>
    public sealed class RetroStatusBar : MonoBehaviour
    {
        [SerializeField] PlayerCharacterController player;
        [SerializeField] PlayerWeaponsManager inventory;
        [SerializeField] StolenWeaponCarrier carrier;
        [SerializeField] ProtagonistMelee melee;
        [SerializeField] Health health;
        [SerializeField] TMP_FontAsset labelFont;
#pragma warning disable 414
        // obsoleto: layout en columnas. Kept because both values are serialized in RequiemCampaign and the labs.
        [SerializeField, Range(.35f, 1f)] float clusterWidth = .53f;
        // obsoleto: layout en columnas.
        [SerializeField, Min(80)] float barHeight = 180;
#pragma warning restore 414

        // Reference-resolution layout (1920x1080, ScaleWithScreenSize). Shared with the editor authoring/measuring tools.
        public const float EdgeMargin = 16, WeaponsWidth = 230, WeaponsTopMargin = 60, BodyWidth = 210;
        /// Fraction of the right column below which it is only an outline: the first-person weapon lives there.
        public const float BodyContentFrom = .45f;
        public const float AvatarWidth = 260, AvatarHeight = 210, AvatarBottom = 12;
        public static readonly Vector2 StatusPosition = new Vector2(EdgeMargin + WeaponsWidth + 24, EdgeMargin);
        public static readonly Vector2 StatusSize = new Vector2(480, 150);
        public static readonly Vector2 PromptPosition = new Vector2(0, AvatarBottom + AvatarHeight + 20);
        public static readonly Vector2 PromptSize = new Vector2(1000, 45);
        public const string NoWeaponName = "SIN ARMA";
        public static readonly string[] OverdriveLabels = { "INACTIVO", "DISPONIBLE", "ACTIVO" };
        public static readonly string[] CoreLabels = { "ÓPTIMO", "DAÑADO", "GRAVE", "CRÍTICO" };
        public static readonly string[] RightArmLabels = { "MÓDULO", "ARMADO", "PUÑO" };
        public static readonly string[] LeftArmLabels = { "LISTO", "CARGANDO", "CARGA MÁX" };
        public static readonly string[] LegsLabels = { "DASH LISTO", "RECARGA", "N/D" };

        RectTransform bodyColumn, weaponsColumn;
        RetroDigitsGraphic energy, life, charge;
        TMP_Text energyLabel, drive, weaponName;
        RobotPortraitGraphic portrait;
        FistChargeRadialGraphic radial;
        readonly UnityEngine.UI.Image[] slots = new UnityEngine.UI.Image[9];
        readonly TMP_Text[] rowValues = new TMP_Text[4];
        readonly UnityEngine.UI.Image[] rowLamps = new UnityEngine.UI.Image[4];
        readonly int[] rowState = { -1, -1, -1, -1 };
        AdvancedPlayerMovement movement;
        ProtagonistHandsPresentation hands;
        WeaponController shownWeapon; WeaponHeat heat; bool weaponShown;
        string shownEnergyText; int shownDrive = -1;
        bool driveRadial;
        Color cyan, orange, steel, text, textDim, danger, panelFill, panelRim;
        TMP_FontAsset titleFont, bodyFont;
        float titleSpacing;
        static readonly Color SlotActive = new Color(.66f, .22f, .04f), SlotOwned = new Color(.16f, .38f, .4f), SlotEmpty = new Color(.04f, .05f, .06f);

        public void Configure(PlayerCharacterController owner, TMP_FontAsset font)
        {
            player=owner; inventory=owner.GetComponent<PlayerWeaponsManager>(); carrier=owner.GetComponent<StolenWeaponCarrier>();
            melee=owner.GetComponent<ProtagonistMelee>(); health=owner.GetComponent<Health>(); labelFont=font;
        }

        void Awake()
        {
            ApplyTheme();
            var root=(RectTransform)transform;
            root.anchorMin=Vector2.zero;root.anchorMax=Vector2.one;root.pivot=new Vector2(.5f,.5f);root.offsetMin=root.offsetMax=Vector2.zero;
            // the previous bar painted a solid backing on this object; the column layout leaves the centre free
            var backing=GetComponent<UnityEngine.UI.Image>();if(backing)backing.enabled=false;
            BuildWeaponsColumn(root);
            BuildBodyColumn(root);
            BuildAvatar(root);
        }

        void ApplyTheme()
        {
            var theme=IronRequiemUiThemeLink.Load();
            if(theme)
            {
                cyan=theme.Cyan;orange=theme.Orange;steel=theme.Steel;text=theme.Text;textDim=theme.TextDim;danger=theme.Danger;
                panelFill=theme.PanelFill;panelRim=theme.PanelRim;titleSpacing=theme.TitleSpacing;
                titleFont=theme.TitleFont;bodyFont=theme.BodyFont;
            }
            else
            {
                cyan=new Color(.15f,.88f,.94f);orange=new Color(1,.38f,.09f);steel=new Color(.33f,.35f,.35f);text=new Color(.79f,.79f,.71f);
                textDim=new Color(.55f,.56f,.5f);danger=new Color(1,.3f,.2f);panelFill=new Color(.11f,.12f,.13f);panelRim=new Color(.32f,.34f,.34f);titleSpacing=0;
            }
            panelFill.a=.85f;
            var fallback=labelFont?labelFont:TMP_Settings.defaultFontAsset;
            if(!bodyFont)bodyFont=fallback;if(!titleFont)titleFont=bodyFont;
        }

        // ------------------------------------------------------------------ left: ARMAS
        void BuildWeaponsColumn(RectTransform root)
        {
            var column=Rect("WeaponsColumn",root,Vector2.zero,Vector2.up,new Vector2(EdgeMargin,EdgeMargin),new Vector2(EdgeMargin+WeaponsWidth,-WeaponsTopMargin));
            weaponsColumn=column;
            Fill(column,panelFill);
            Frame("Frame",column,4,2,panelRim);
            Bar("MetalBevel",Rect("TopBevel",column,new Vector2(0,1),Vector2.one,new Vector2(2,-5),new Vector2(-2,-2)),orange);
            Rivets(column,6);
            const float pad=12;
            Label(Top("Title",column,10,28,pad),"ARMAS",22,text,true,TextAlignmentOptions.Left);
            Bar("MetalBevel",Top("Rule",column,42,2,pad),steel);
            Label(Top("CurrentLabel",column,50,16,pad),"ARMA ACTUAL",12,textDim,false,TextAlignmentOptions.Left);
            weaponName=Label(Top("WeaponName",column,68,26,pad),NoWeaponName,18,cyan,false,TextAlignmentOptions.Left);
            var grid=Top("WeaponSlots",column,104,174,pad);
            for(int i=0;i<9;i++)
            {
                float x=(i%3)/3f;int row=i/3;
                var slot=Rect("WeaponSlot_"+(i+1),grid,new Vector2(x,1-(row+1)/3f),new Vector2(x+1f/3,1-row/3f),new Vector2(3,3),new Vector2(-3,-3));
                slots[i]=slot.gameObject.AddComponent<UnityEngine.UI.Image>();slots[i].raycastTarget=false;slots[i].color=SlotEmpty;
                var digitRect=Rect("Digit",slot,Vector2.zero,Vector2.one,new Vector2(14,8),new Vector2(-14,-8));
                var digit=digitRect.gameObject.AddComponent<RetroDigitsGraphic>();digit.raycastTarget=false;digit.SetValue(i+1);digit.color=text;
            }
            // energy / heat and OVERDRIVE are anchored to the bottom of the column; the gap between grows with height
            Label(Bottom("ExpelHint",column,14,18,pad),"[G] EXPULSAR",12,textDim,false,TextAlignmentOptions.Left);
            drive=Label(Bottom("OverdriveState",column,34,30,pad),OverdriveLabels[0],22,textDim,false,TextAlignmentOptions.Left);
            Label(Bottom("OverdriveTitle",column,66,24,pad),"OVERDRIVE",18,text,true,TextAlignmentOptions.Left);
            Bar("MetalBevel",Bottom("Rule",column,96,2,pad),steel);
            energyLabel=Label(Bottom("EnergyState",column,104,20,pad),"SIN MÓDULO",14,textDim,false,TextAlignmentOptions.Left);
            energy=Number(Bottom("EnergyValue",column,128,56,pad),orange);
            Label(Bottom("EnergyTitle",column,190,22,pad),"ENERGÍA / CALOR",16,text,true,TextAlignmentOptions.Left);
            Bar("MetalBevel",Bottom("Rule",column,218,2,pad),steel);
        }

        // ------------------------------------------------------------------ right: ESTADO DEL CUERPO
        void BuildBodyColumn(RectTransform root)
        {
            var column=Rect("BodyColumn",root,Vector2.right,Vector2.one,new Vector2(-EdgeMargin-BodyWidth,EdgeMargin),new Vector2(-EdgeMargin,-EdgeMargin));
            // below BodyContentFrom the column is outline only, so the first-person weapon stays visible
            var content=Rect("BodyContent",column,new Vector2(0,BodyContentFrom),Vector2.one,new Vector2(3,0),new Vector2(-3,-3));
            var fill=content.gameObject.AddComponent<RoundedFrameGraphic>();fill.raycastTarget=false;fill.color=panelFill;fill.Configure(15,0,true,true,false);
            // the rim wraps the content only: an outline down to the screen edge crossed the mounted cannon at 16:10
            Frame("Frame",content,18,3,panelRim);
            bodyColumn=column;
            Bar("MetalBevel",Rect("ContentEdge",content,Vector2.zero,Vector2.right,Vector2.zero,new Vector2(0,2)),steel);
            const float pad=12;
            Label(Top("Title",content,12,26,pad),"SALUD",20,text,true,TextAlignmentOptions.Center);
            life=Number(Top("HealthValue",content,40,52,pad),orange);
            Bar("MetalBevel",Top("Rule",content,98,2,pad),steel);
            Label(Top("BodyTitle",content,106,20,pad),"ESTADO DEL CUERPO",15,text,true,TextAlignmentOptions.Center);
            string[] parts={"NÚCLEO","BRAZO DER.","BRAZO IZQ.","PIERNAS"};
            for(int i=0;i<4;i++)
            {
                var row=Top("BodyRow_"+i,content,130+i*28,26,pad);
                var lamp=Rect("Indicator",row,new Vector2(0,.5f),new Vector2(0,.5f),new Vector2(0,-5),new Vector2(10,5));
                rowLamps[i]=lamp.gameObject.AddComponent<UnityEngine.UI.Image>();rowLamps[i].raycastTarget=false;rowLamps[i].color=textDim;
                Label(Rect("Part",row,Vector2.zero,new Vector2(.5f,1),new Vector2(16,0),Vector2.zero),parts[i],13,text,false,TextAlignmentOptions.Left);
                rowValues[i]=Label(Rect("Value",row,new Vector2(.5f,0),Vector2.one,new Vector2(4,0),Vector2.zero),"",13,textDim,false,TextAlignmentOptions.Right);
            }
            Bar("MetalBevel",Top("Rule",content,246,2,pad),steel);
            Label(Top("ChargeTitle",content,254,20,pad),"CARGA PUÑO",15,text,true,TextAlignmentOptions.Center);
            // the ring takes the space left between the title and the percentage, up to 130 px, always square
            var area=Rect("FistChargeArea",content,new Vector2(.5f,0),new Vector2(.5f,1),new Vector2(-65,48),new Vector2(65,-280));
            var ring=Rect("FistChargeRadial",area,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(-65,-65),new Vector2(65,65));
            var fit=ring.gameObject.AddComponent<AspectRatioFitter>();fit.aspectMode=AspectRatioFitter.AspectMode.FitInParent;fit.aspectRatio=1;
            radial=ring.gameObject.AddComponent<FistChargeRadialGraphic>();radial.raycastTarget=false;radial.color=cyan;
            charge=Number(Bottom("ChargeValue",content,26,20,pad),cyan);
            Label(Bottom("ChargeHint",content,8,16,pad),"[F] MANTENER",12,textDim,false,TextAlignmentOptions.Center);
        }

        // ------------------------------------------------------------------ bottom centre: UNIDAD 01
        void BuildAvatar(RectTransform root)
        {
            var box=Rect("AvatarFrame",root,new Vector2(.5f,0),new Vector2(.5f,0),Vector2.zero,Vector2.zero);
            box.pivot=new Vector2(.5f,0);box.sizeDelta=new Vector2(AvatarWidth,AvatarHeight);box.anchoredPosition=new Vector2(0,AvatarBottom);
            // 16/11 (user): only the avatar, no box around it. AvatarFrame stays as an invisible container (tools find it by name).
            var face=Rect("RobotPortrait",box,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,Vector2.zero);
            face.sizeDelta=new Vector2(140,140);face.anchoredPosition=Vector2.zero;
            portrait=face.gameObject.AddComponent<RobotPortraitGraphic>();portrait.raycastTarget=false;
        }

        void Start()
        {
            portrait.Bind(health,inventory,carrier,melee);
            var owner=player?player.gameObject:health?health.gameObject:null;
            if(owner)
            {
                movement=owner.GetComponent<AdvancedPlayerMovement>();
                hands=owner.GetComponent<ProtagonistHandsPresentation>();if(!hands)hands=owner.GetComponentInChildren<ProtagonistHandsPresentation>(true);
            }
            var fistHud=Object.FindAnyObjectByType<FistChargeHud>(FindObjectsInactive.Include);
            if(fistHud)fistHud.AttachRadial(radial);
            else{driveRadial=true;radial.Driven=true;}
            PlaceMechanicTexts();
            // the heat gauge sat in the bottom-right corner, over the weapon: dock it just under the body content
            var heatHud=Object.FindAnyObjectByType<WeaponHeatHud>(FindObjectsInactive.Include);
            if(heatHud&&bodyColumn)heatHud.Dock(bodyColumn,new Vector2(.5f,BodyContentFrom),new Vector2(.5f,1),new Vector2(0,-HeatDockGap),BodyWidth-8);
        }
        public const float HeatDockGap = 10;
        /// Band of the ARMAS column (from its top) that receives the template ammo counters: below the slot grid (104 + 174).
        public const float AmmoTop = 290, AmmoHeight = 130;
        /// Toasts ("ARMA OBTENIDA · …") stack above the mechanic status texts, beside the left column instead of over it.
        public static readonly Vector2 NotificationPosition = new Vector2(StatusPosition.x, StatusPosition.y + StatusSize.y + 8);

        /// MechanicHud keeps its own texts; with this layout they move beside the left column and above the avatar.
        void PlaceMechanicTexts()
        {
            // template NotificationHUDManager panel (fps.UI is not referenced by this assembly, so it is found by name)
            var canvas=GetComponentInParent<Canvas>();
            // template WeaponHUDManager ammo counters stacked in the bottom-right corner, over the weapon: they move into
            // the empty band of the ARMAS column under the nine slots
            RectTransform notes=null,ammo=null;
            if(canvas)foreach(var r in canvas.rootCanvas.GetComponentsInChildren<RectTransform>(true))
            {
                if(!notes&&r.name=="NotificationsRect")notes=r;
                else if(!ammo&&r.name=="WeaponHUDManager")ammo=r;
            }
            if(notes){notes.SetParent(transform,false);notes.anchorMin=notes.anchorMax=notes.pivot=Vector2.zero;notes.anchoredPosition=NotificationPosition;}
            if(ammo&&weaponsColumn)
            {
                ammo.SetParent(weaponsColumn,false);ammo.anchorMin=new Vector2(0,1);ammo.anchorMax=Vector2.one;ammo.pivot=new Vector2(.5f,1);
                ammo.offsetMin=new Vector2(12,-AmmoTop-AmmoHeight);ammo.offsetMax=new Vector2(-12,-AmmoTop);
            }
            var mechanic=Object.FindAnyObjectByType<MechanicHud>(FindObjectsInactive.Include);if(!mechanic)return;
            foreach(var r in mechanic.GetComponentsInChildren<RectTransform>(true))
            {
                if(r.name=="MechanicStatus")Place(r,Vector2.zero,StatusPosition,StatusSize);
                else if(r.name=="InteractionPrompt")Place(r,new Vector2(.5f,0),PromptPosition,PromptSize);
            }
        }
        static void Place(RectTransform r,Vector2 anchor,Vector2 position,Vector2 size)
        {
            r.anchorMin=r.anchorMax=anchor;r.pivot=new Vector2(anchor.x,0);r.anchoredPosition=position;r.sizeDelta=size;
        }

        void LateUpdate()
        {
            if(!health||!inventory)return;
            var active=inventory.GetActiveWeapon();var module=carrier?carrier.Held:null;
            if(!weaponShown||!ReferenceEquals(active,shownWeapon))
            {
                // names and the heat owner are resolved once per weapon change, never per frame
                weaponShown=true;shownWeapon=active;heat=active?active.GetComponent<WeaponHeat>():null;
                weaponName.text=active?CleanName(active.name):NoWeaponName;
            }
            bool held=module&&module.EquippedWeapon==active;
            bool thermal=heat&&heat.HeatEnabled;
            energy.SetValue(held?Mathf.CeilToInt(module.Ammo.Remaining):thermal?Mathf.RoundToInt(heat.NormalizedHeat*100):0,thermal&&!held);
            string energyText=held?"ENERGÍA DEL MÓDULO":thermal?heat.StateLabel:"SIN MÓDULO";
            if(!ReferenceEquals(energyText,shownEnergyText))
            {
                shownEnergyText=energyText;energyLabel.text=energyText;
                energyLabel.color=energyText=="SOBRECALENTADA"?danger:energyText=="CALOR ALTO"?orange:held?text:textDim;
            }
            float ratio=health.GetRatio();
            life.SetValue(Mathf.CeilToInt(ratio*100),true);
            life.color=ratio<.25f?Color.Lerp(orange,text,.5f+.5f*Mathf.Sin(Time.unscaledTime*7)):orange;
            bool charging=melee&&melee.IsCharging,full=charging&&melee.FullyCharged;float level=charging?melee.ChargeLevel:0;
            charge.SetValue(Mathf.RoundToInt(level*100),true);
            if(driveRadial)radial.SetState(charging,level,full);
            bool overdrive=module&&module.Overdrive.IsArmed;
            int driveState=overdrive?2:module?1:0;
            if(driveState!=shownDrive){shownDrive=driveState;drive.text=OverdriveLabels[driveState];drive.color=driveState==2?orange:driveState==1?cyan:textDim;}
            // O(9), the inventory's fixed slot limit. No collection or string construction per frame.
            for(int i=0;i<9;i++)
            {
                var weapon=inventory.GetWeaponAtSlotIndex(i);
                slots[i].color=weapon&&weapon==active?SlotActive:weapon?SlotOwned:SlotEmpty;
            }
            // body rows: only states the game really tracks (health ratio, arm module, fist charge, dash)
            int core=ratio>=.75f?0:ratio>=.5f?1:ratio>=.25f?2:3;
            Row(0,core,CoreLabels[core],core==0?cyan:core==3?danger:orange);
            int right=(hands&&hands.RightForearmReplaced)||(held&&active)?0:active?1:2;
            Row(1,right,RightArmLabels[right],right==0?orange:right==1?text:cyan);
            int left=full?2:charging?1:0;
            Row(2,left,LeftArmLabels[left],left==0?cyan:left==1?orange:text);
            int legs=movement?(movement.DashAvailable?0:1):2;
            Row(3,legs,LegsLabels[legs],legs==0?cyan:legs==1?orange:textDim);
        }
        void Row(int index,int state,string value,Color tint)
        {
            if(rowState[index]==state)return;
            rowState[index]=state;rowValues[index].text=value;rowValues[index].color=tint;rowLamps[index].color=tint;
        }
        /// "Weapon_IR_ArmCannon_VO(Clone)" -> "ARMCANNON VO". Called only when the active weapon changes.
        public static string CleanName(string raw)
        {
            string n=raw.Replace("(Clone)","").Trim();
            if(n.StartsWith("Weapon_IR_",System.StringComparison.Ordinal))n=n.Substring(10);
            else if(n.StartsWith("Weapon_",System.StringComparison.Ordinal))n=n.Substring(7);
            n=n.Replace('_',' ').Trim().ToUpperInvariant();
            return n.Length>0?n:NoWeaponName;
        }

        // ------------------------------------------------------------------ builders
        RetroDigitsGraphic Number(RectTransform r,Color tint)
        {
            var value=r.gameObject.AddComponent<RetroDigitsGraphic>();value.color=tint;value.raycastTarget=false;return value;
        }
        TMP_Text Label(RectTransform r,string value,float size,Color tint,bool title,TextAlignmentOptions alignment)
        {
            var label=r.gameObject.AddComponent<TextMeshProUGUI>();label.font=title?titleFont:bodyFont;
            label.text=value;label.fontSize=size;label.fontStyle=FontStyles.Bold;label.alignment=alignment;
            label.color=tint;label.raycastTarget=false;label.textWrappingMode=TextWrappingModes.NoWrap;label.overflowMode=TextOverflowModes.Overflow;
            if(title)label.characterSpacing=titleSpacing*.5f;
            // shrink-to-fit only: the authored size is the maximum, long weapon names and states scale down
            label.enableAutoSizing=true;label.fontSizeMax=size;label.fontSizeMin=Mathf.Max(9,size*.6f);return label;
        }
        static void Fill(RectTransform r,Color tint)
        {
            var image=r.gameObject.AddComponent<UnityEngine.UI.Image>();image.color=tint;image.raycastTarget=false;
        }
        static void Frame(string name,RectTransform parent,float radius,float thickness,Color tint)
        {
            var r=Rect(name,parent,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
            var frame=r.gameObject.AddComponent<RoundedFrameGraphic>();frame.raycastTarget=false;frame.color=tint;frame.Configure(radius,thickness,false);
        }
        static void Bar(string name,RectTransform r,Color tint)
        {
            r.name=name;var image=r.gameObject.AddComponent<UnityEngine.UI.Image>();image.color=tint;image.raycastTarget=false;
        }
        void Rivets(RectTransform parent,float inset)
        {
            for(int i=0;i<4;i++)
            {
                var corner=new Vector2(i%2,i/2);
                var screw=Rect("Rivet",parent,corner,corner,Vector2.zero,Vector2.zero);
                screw.sizeDelta=new Vector2(6,6);screw.anchoredPosition=new Vector2(corner.x>0?-inset:inset,corner.y>0?-inset-4:inset);
                var bolt=screw.gameObject.AddComponent<UnityEngine.UI.Image>();bolt.color=steel*new Color(1,1,1,.7f);bolt.raycastTarget=false;
            }
        }
        static RectTransform Top(string name,RectTransform parent,float top,float height,float pad)=>
            Rect(name,parent,new Vector2(0,1),Vector2.one,new Vector2(pad,-top-height),new Vector2(-pad,-top));
        static RectTransform Bottom(string name,RectTransform parent,float bottom,float height,float pad)=>
            Rect(name,parent,Vector2.zero,Vector2.right,new Vector2(pad,bottom),new Vector2(-pad,bottom+height));
        static RectTransform Rect(string name,Transform parent,Vector2 min,Vector2 max,Vector2 offsetMin,Vector2 offsetMax)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer));go.layer=parent.gameObject.layer;var r=(RectTransform)go.transform;r.SetParent(parent,false);
            r.anchorMin=min;r.anchorMax=max;r.offsetMin=offsetMin;r.offsetMax=offsetMax;return r;
        }
    }
}
