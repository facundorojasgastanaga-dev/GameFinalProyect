using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
namespace IronRequiem.Tests
{
    public sealed class CannonBurrSmokeRelay : MonoBehaviour
    {
        [Serializable] public sealed class Check {public string name;public bool passed;public string failure;}
        [Serializable] public sealed class Report {public string mode="In-Play native-input checks; not Test Runner";public List<Check> checks=new List<Check>();}
        readonly Report report=new Report();bool failed;Check current;InputFocusTestScope focus;
        void Awake(){DontDestroyOnLoad(gameObject);}
        IEnumerator Start()
        {
            focus=new InputFocusTestScope();focus.Begin();
            yield return Case("Clean equipped mesh; mouth/charge alignment during movement and aim",s=>s.RimChargeAndAimRemainAlignedThroughMotionAndAds());
            yield return Case("Pickup, consecutive charged shots, energy consumption, no self collision",s=>s.ConsecutiveShotsStartAtMouthWhileMovingAndIgnorePlayer());
            focus.End();focus=null;
            File.WriteAllText("Tools/IronRequiem/Reports/CannonBurr/play-smoke.json",JsonUtility.ToJson(report,true));
#if UNITY_EDITOR
            SessionState.SetBool("IRON.CannonBurr.done",true);
#endif
        }
        IEnumerator Case(string name,Func<CannonChargeBehaviorTests,IEnumerator> body)
        {
            current=new Check{name=name};failed=false;var suite=new CannonChargeBehaviorTests();
            yield return Checked(suite.Setup());
            if(!failed)yield return Checked(ValidateMesh());
            if(!failed)yield return Checked(body(suite));
            yield return Checked(suite.Cleanup());current.passed=!failed;report.checks.Add(current);
        }
        IEnumerator ValidateMesh()
        {
            var filter=UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single(x=>x.name=="StolenCannonVisual");
            Assert.That(filter.sharedMesh.name,Is.EqualTo("CannonClean_LOD0"));
            Assert.That(filter.sharedMesh.subMeshCount,Is.EqualTo(2));
            Assert.That(filter.GetComponent<Renderer>().sharedMaterials.Length,Is.EqualTo(2));
            yield return null;
        }
        IEnumerator Checked(IEnumerator routine)
        {
            var stack=new Stack<IEnumerator>();stack.Push(routine);
            while(stack.Count>0)
            {
                var top=stack.Peek();bool more=false;object value=null;
                try{more=top.MoveNext();if(more)value=top.Current;}
                catch(Exception e){failed=true;current.failure=(current.failure??"")+e+"\n";yield break;}
                if(!more){stack.Pop();continue;}
                if(value is IEnumerator nested)stack.Push(nested);else yield return value;
            }
        }
        void OnDestroy(){if(focus!=null)focus.End();}
    }
}
