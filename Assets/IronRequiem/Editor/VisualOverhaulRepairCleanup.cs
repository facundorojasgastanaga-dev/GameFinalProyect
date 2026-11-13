using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace IronRequiem.Editor
{
    public static class VisualOverhaulRepairCleanup
    {
        static string Sha(string path){using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();}
        public static string Inspect()
        {
            VisualOverhaulRepairApply.Guard();string report=VisualOverhaulRepair.Report;
            var candidates=File.ReadAllLines(report+"/cleanup-candidates.txt").Where(File.Exists).Distinct().ToArray();
            var roots=AssetDatabase.GetAllAssetPaths().Where(p=>p.StartsWith("Assets/")&&!AssetDatabase.IsValidFolder(p)&&!candidates.Contains(p)).ToArray();
            var needed=AssetDatabase.GetDependencies(roots,true).ToHashSet();var deleted=candidates.Where(p=>!needed.Contains(p)).ToArray();var retained=candidates.Where(needed.Contains).ToArray();
            File.WriteAllLines(report+"/cleanup-native-confirmed.txt",deleted);File.WriteAllLines(report+"/cleanup-native-retained.txt",retained);
            return "Native AssetDatabase transitive dependency check: roots="+roots.Length+", confirmed unused="+deleted.Length+", retained="+retained.Length+" (including binary lighting/importer dependencies). No deletion yet.";
        }
        public static string DeleteConfirmed()
        {
            VisualOverhaulRepairApply.Guard();string report=VisualOverhaulRepair.Report,backup="Tools/IronRequiem/Backups/20261108-visual-repair/DeletedAssets";
            var candidates=File.ReadAllLines(report+"/cleanup-native-confirmed.txt");if(candidates.Any(p=>p.EndsWith(".prefab")||p.EndsWith(".fbx")||p.EndsWith(".unity")||!p.StartsWith("Assets/IronRequiem/Art/VisualOverhaul/")))throw new Exception("Unexpected candidate; inspect separately.");
            var roots=AssetDatabase.GetAllAssetPaths().Where(p=>p.StartsWith("Assets/")&&!AssetDatabase.IsValidFolder(p)&&!candidates.Contains(p)).ToArray();var needed=AssetDatabase.GetDependencies(roots,true).ToHashSet();
            if(candidates.Any(needed.Contains))throw new Exception("Dependencies changed; regenerate candidate inventory.");
            Directory.CreateDirectory(backup);var manifest=new StringBuilder("originalPath\tGUID\tSHA256\tmetaSHA256\n");
            foreach(string path in candidates)
            {
                string target=backup+"/"+path;if(File.Exists(target))throw new Exception("Backup already exists: "+path);Directory.CreateDirectory(Path.GetDirectoryName(target));File.Copy(path,target);File.Copy(path+".meta",target+".meta");manifest.AppendLine(path+"\t"+AssetDatabase.AssetPathToGUID(path)+"\t"+Sha(path)+"\t"+Sha(path+".meta"));
            }
            File.WriteAllText(backup+"/manifest.tsv",manifest.ToString());File.WriteAllText(backup+"/Restore.ps1",@"param([string]$ProjectRoot=(Resolve-Path (Join-Path $PSScriptRoot '../../../../..')).Path)
$entries=Import-Csv -LiteralPath (Join-Path $PSScriptRoot 'manifest.tsv') -Delimiter ""`t""
foreach($entry in $entries){
  $target=[IO.Path]::GetFullPath((Join-Path $ProjectRoot $entry.originalPath))
  if(-not $target.StartsWith(([IO.Path]::GetFullPath($ProjectRoot).TrimEnd('\')+'\'),[StringComparison]::OrdinalIgnoreCase)){throw 'Path outside project'}
  if((Test-Path -LiteralPath $target) -or (Test-Path -LiteralPath ($target+'.meta'))){throw ('Refuse to overwrite '+$target)}
}
foreach($entry in $entries){
  $source=Join-Path $PSScriptRoot $entry.originalPath;$target=Join-Path $ProjectRoot $entry.originalPath
  if((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.SHA256){throw 'Backup hash mismatch'}
  if((Get-FileHash -LiteralPath ($source+'.meta') -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.metaSHA256){throw 'Meta hash mismatch'}
  New-Item -ItemType Directory -Path (Split-Path $target) -Force | Out-Null
  Copy-Item -LiteralPath $source -Destination $target;Copy-Item -LiteralPath ($source+'.meta') -Destination ($target+'.meta')
}
Write-Output 'Restored exact files and meta GUIDs. Refresh Unity.'
");
            var actual=new StringBuilder();foreach(string path in candidates){if(!AssetDatabase.DeleteAsset(path))throw new Exception("Delete failed "+path);actual.AppendLine(path);}AssetDatabase.SaveAssets();File.WriteAllText(report+"/deleted-assets.txt",actual.ToString());
            return "Deleted via AssetDatabase.DeleteAsset: "+candidates.Length+" assets. Exact bytes/meta/GUID backup and Restore.ps1: "+backup;
        }
    }
}
