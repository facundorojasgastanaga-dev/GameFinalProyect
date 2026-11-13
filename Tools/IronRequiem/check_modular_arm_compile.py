"""Compile into a task folder using Unity's existing compiler/response files; never touch Editor DLLs."""
import json, re, subprocess
from pathlib import Path

root=Path(__file__).resolve().parents[2]
output=root/'Tools/IronRequiem/Reports/ModularArm/Compile'
output.mkdir(parents=True,exist_ok=True)
editor=Path('C:/Program Files/Unity/Hub/Editor/6000.5.5f1/Editor/Data')
dotnet=editor/'NetCoreRuntime/dotnet.exe'
compiler=editor/'DotNetSdk/sdk/8.0.318/Roslyn/bincore/csc.dll'
bee=root/'Library/Bee/artifacts/1900b0aE.dag'
results=[]
for assembly,folder in [('IronRequiem.Runtime','Assets/IronRequiem/Scripts'),
                        ('IronRequiem.Editor','Assets/IronRequiem/Editor'),
                        ('IronRequiem.PlayModeTests','Assets/IronRequiem/Tests/PlayMode')]:
    text=(bee/(assembly+'.rsp')).read_text(encoding='utf-8-sig')
    text=re.sub(r'^-out:.*$', '-out:"'+(output/(assembly+'.dll')).as_posix()+'"',text,flags=re.M)
    text=re.sub(r'^-refout:.*$', '-refout:"'+(output/(assembly+'.ref.dll')).as_posix()+'"',text,flags=re.M)
    for name in ['IronRequiem.Runtime','IronRequiem.Editor']:
        if (output/(name+'.ref.dll')).exists():
            text=text.replace('Library/Bee/artifacts/1900b0aE.dag/'+name+'.ref.dll',(output/(name+'.ref.dll')).as_posix())
    for path in (root/folder).rglob('*.cs'):
        rel=path.relative_to(root).as_posix()
        if rel not in text.replace('\\','/'):
            text+='\n"'+rel+'"\n'
    response=output/(assembly+'.rsp'); response.write_text(text,encoding='utf-8')
    completed=subprocess.run([str(dotnet),str(compiler),'@'+str(response)],cwd=root,capture_output=True,text=True)
    log=completed.stdout+completed.stderr
    (output/(assembly+'.log')).write_text(log,encoding='utf-8')
    errors=[line for line in log.splitlines() if 'error ' in line]
    results.append({'assembly':assembly,'exit_code':completed.returncode,'errors':errors})
    print(assembly, 'exit='+str(completed.returncode))
    for line in errors: print(line)
    if completed.returncode: break
(output/'result.json').write_text(json.dumps({'mode':'Offline compiler check using existing Unity compiler and assembly references; NOT an Editor import or Play Mode test','assemblies':results},indent=2),encoding='utf-8')
raise SystemExit(0 if len(results)==3 and all(r['exit_code']==0 for r in results) else 1)
