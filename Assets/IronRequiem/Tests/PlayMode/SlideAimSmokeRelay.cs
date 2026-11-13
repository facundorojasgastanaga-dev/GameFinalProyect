using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
namespace IronRequiem.Tests
{
    // Temporary in-Play verifier. Enter/exit Play preserves the user's unsaved Edit Mode scene.
    // Uses the same NUnit assertions/input fixtures, without Test Runner's SaveModifiedSceneTask.
    public sealed class SlideAimSmokeRelay : MonoBehaviour
    {
        [Serializable] public sealed class Check {public string name;public bool passed;public string failure;}
        [Serializable] public sealed class Report {public string mode="In-Play smoke checks with native Input System and NUnit assertions; not a Test Runner run";public List<Check> checks=new List<Check>();}
        readonly Report report=new Report();bool failed;Check current;InputFocusTestScope focus;
        void Awake(){DontDestroyOnLoad(gameObject);}
        IEnumerator Start()
        {
            focus=new InputFocusTestScope();focus.Begin();
            yield return Case("Pistol idle and repeated fire without inherited energy particles",s=>s.PistolHasNoTemplateEnergyLoopsBeforeAndAfterFire());
            yield return Case("Slide steadiness, aiming, mouse look, shooting and walking bob recovery",s=>s.SlideKeepsWeaponSteadyAllowsAimAndFireThenRestoresWalkingBob());
            focus.End();focus=null;
            File.WriteAllText("Tools/IronRequiem/Reports/SlideAim/play-smoke.json",JsonUtility.ToJson(report,true));
#if UNITY_EDITOR
            SessionState.SetBool("IRON.SlideAim.done",true);
#endif
        }
        IEnumerator Case(string name,Func<SlidePistolMainTests,IEnumerator> body)
        {
            current=new Check{name=name};failed=false;var suite=new SlidePistolMainTests();
            yield return Checked(suite.Setup());
            if(!failed)yield return Checked(body(suite));
            yield return Checked(suite.Cleanup());current.passed=!failed;report.checks.Add(current);
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
