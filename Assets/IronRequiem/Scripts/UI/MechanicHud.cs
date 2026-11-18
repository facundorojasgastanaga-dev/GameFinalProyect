using TMPro;
using UnityEngine;

namespace IronRequiem
{
    public sealed class MechanicHud : MonoBehaviour
    {
        [SerializeField] AdvancedPlayerMovement movement;
        [SerializeField] StolenWeaponCarrier carrier;
        [SerializeField] TMP_Text status;
        [SerializeField] TMP_Text prompt;
        [SerializeField] bool compactStatus;
        public void ConfigureCompactStatus(bool compact)=>compactStatus=compact;
        bool lastDash, lastArmed, lastActive, lastBurst, lastSaw, lastUnarmed, initialized;
        ProtagonistMelee melee;
        int lastEnergy = int.MinValue, lastCharge = -1, lastSeconds = -1, lastPromptSeconds = -1;
        EnemyExecutionSource lastExecution;CannonPickupStatus lastExecutionStatus;
        StolenCannon lastCandidate;
        CannonPickupStatus lastPickupStatus;
        public void Configure(AdvancedPlayerMovement motion, StolenWeaponCarrier holder, TMP_Text statusText, TMP_Text promptText)
        { movement = motion; carrier = holder; status = statusText; prompt = promptText; }
        void LateUpdate()
        {
            if (!carrier) return;
            if (!melee) melee = carrier.GetComponent<ProtagonistMelee>();
            bool unarmed = melee && melee.Unarmed;
            var module = carrier.Held;
            bool dash = movement && movement.DashAvailable, armed = module && module.Overdrive.IsArmed, active = carrier.CanUseHeldWeapon;
            int energy = module ? Mathf.CeilToInt(module.Ammo.Remaining) : -1;
            var charging = module && module.EquippedWeapon ? module.EquippedWeapon.GetComponent<CannonChargeShot>() : null;
            bool burst = module && module.EquippedWeapon && module.EquippedWeapon.GetComponent<CannonBurstShot>();
            bool saw = module && module.WeaponPrefab && module.WeaponPrefab.GetComponent<StolenSawAttack>();
            int charge = charging ? Mathf.RoundToInt(charging.ChargeFraction * 100) : 0;
            int seconds = armed ? Mathf.CeilToInt(module.Overdrive.RemainingDangerSeconds * 10) : 0;
            if (!initialized || dash != lastDash || energy != lastEnergy || armed != lastArmed || active != lastActive || charge != lastCharge || seconds != lastSeconds || burst != lastBurst || saw != lastSaw || unarmed != lastUnarmed)
            {
                // v5: right-hand strike on F (hold to charge); unarmed, M1 strikes too
                if(compactStatus)
                {
                    if(!initialized||dash!=lastDash||unarmed!=lastUnarmed)
                        status.text=unarmed?(dash?"CTRL  DASH LISTO\nF  CARGAR PUÑO  ·  M1  GOLPE DERECHO":"CTRL  DASH RECARGANDO\nF  CARGAR PUÑO  ·  M1  GOLPE DERECHO"):
                            (dash?"CTRL  DASH LISTO\nF  CARGAR ANTEBRAZO  ·  G  EXPULSAR":"CTRL  DASH RECARGANDO\nF  CARGAR ANTEBRAZO  ·  G  EXPULSAR");
                }
                else status.text = (movement ? "DASH  " + (dash ? "LISTO [CTRL]" : "SIN CARGA") : "") +
                    (melee ? "\nGOLPE IZQ.  [F]" + (unarmed ? " · DER. [M1]" : "") + " · MANTENER [F] CARGA" : "") +
                    (energy >= 0 ? "\nENERGÍA  " + energy + "/" + Mathf.CeilToInt(module.Ammo.Capacity) + (active ? "" : "  (guardado)") +
                    "\n" + (armed ? "PELIGRO · OVERDRIVE " + (seconds / 10f).ToString("0.0") + " s\n[G] LANZAR AHORA" : saw ? "MOTOSIERRA · MANTENER [M1]\n[G] SOLTAR" : burst ? "RÁFAGA · 3 PROYECTILES [M1]\n[G] SOLTAR" : "CARGA  " + charge + "% [M1]\n[G] SOLTAR") : "");
                status.color = armed ? new Color(1, .55f, .15f) : Color.white;
                lastUnarmed = unarmed; lastSaw = saw; lastBurst = burst; lastDash = dash; lastEnergy = energy; lastArmed = armed; lastActive = active; lastCharge = charge; lastSeconds = seconds;
            }
            int promptSeconds = carrier.Candidate && carrier.Candidate.Overdrive.IsArmed ? Mathf.CeilToInt(carrier.Candidate.Overdrive.RemainingDangerSeconds) : -1;
            if (!initialized || carrier.ExecutionCandidate!=lastExecution || carrier.ExecutionStatus!=lastExecutionStatus || carrier.Candidate != lastCandidate || promptSeconds != lastPromptSeconds || carrier.PickupStatus != lastPickupStatus)
            {
                lastCandidate = carrier.Candidate; lastPromptSeconds = promptSeconds; lastPickupStatus=carrier.PickupStatus;
                string message=lastPickupStatus==CannonPickupStatus.Ready?"[E] RECOGER CAÑÓN":
                    lastPickupStatus==CannonPickupStatus.TooFar?"ACÉRCATE AL CAÑÓN (2,5 m)":
                    lastPickupStatus==CannonPickupStatus.CoolingDown?"CAÑÓN DESPRENDIÉNDOSE…":
                    lastPickupStatus==CannonPickupStatus.InventoryFull?"INVENTARIO LLENO · LIBERA UN HUECO":
                    lastPickupStatus==CannonPickupStatus.AlreadyHolding?"YA LLEVAS UN CAÑÓN · [G] SOLTAR":
                    lastPickupStatus==CannonPickupStatus.Invalid?"CAÑÓN NO DISPONIBLE":"";
                if(lastCandidate&&lastCandidate.WeaponPrefab&&lastCandidate.WeaponPrefab.GetComponent<StolenSawAttack>())message=message.Replace("CAÑÓN","MOTOSIERRA").Replace("AL MOTOSIERRA","A LA MOTOSIERRA").Replace("UN MOTOSIERRA","UNA MOTOSIERRA");
                if(saw)message=message.Replace("CAÑÓN","ARMA");
                prompt.text = lastCandidate ? message + (promptSeconds >= 0 ? " · PELIGRO " + promptSeconds + " s" : "") : "";
                lastExecution=carrier.ExecutionCandidate;lastExecutionStatus=carrier.ExecutionStatus;
                if(lastExecution)prompt.text=lastExecutionStatus==CannonPickupStatus.Ready?"[E] Ejecutar y tomar arma":lastExecutionStatus==CannonPickupStatus.TooFar?"ACÉRCATE PARA EJECUTAR":lastExecutionStatus==CannonPickupStatus.AlreadyHolding?"YA LLEVAS UN CAÑÓN · [G] SOLTAR":lastExecutionStatus==CannonPickupStatus.InventoryFull?"INVENTARIO LLENO · LIBERA UN HUECO":"ARMA NO DISPONIBLE";
                if(saw)prompt.text=prompt.text.Replace("CAÑÓN","ARMA");
                prompt.color = promptSeconds >= 0 ? new Color(1, .55f, .15f) : Color.white;
            }
            initialized = true;
        }
    }
}
