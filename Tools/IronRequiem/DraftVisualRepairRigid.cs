using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace IronRequiem.Editor
{
    public static class VisualOverhaulRepairRigid
    {
        struct Edge {public int a,b,bone;public Edge(int x,int y,int n){a=x;b=y;bone=n;}}
        static int Dominant(BoneWeight[] weights,int a,int b,int c)
        {
            var score=new Dictionary<int,float>();foreach(int v in new[]{a,b,c})
            {
                var w=weights[v];int[] bones={w.boneIndex0,w.boneIndex1,w.boneIndex2,w.boneIndex3};float[] values={w.weight0,w.weight1,w.weight2,w.weight3};
                for(int i=0;i<4;i++){if(!score.ContainsKey(bones[i]))score[bones[i]]=0;score[bones[i]]+=values[i];}
            }
            return score.OrderByDescending(p=>p.Value).ThenBy(p=>p.Key).First().Key;
        }
        static Mesh Build(Mesh source,string name,out int caps,out int unclosed)
        {
            var vertices=source.vertices;var normals=source.normals;var uv=source.uv;var sourceWeights=source.boneWeights;
            if(source.blendShapeCount!=0||vertices.Length!=sourceWeights.Length)throw new Exception("Unsupported skin; preserve original "+source.name);
            var positions=new List<Vector3>();var n=new List<Vector3>();var u=new List<Vector2>();var weights=new List<BoneWeight>();var sub=new List<List<int>>();
            var map=new Dictionary<(int,int),int>();var weld=new Dictionary<Vector3Int,int>();var canonical=new int[vertices.Length];
            for(int i=0;i<vertices.Length;i++){var q=Vector3Int.RoundToInt(vertices[i]*10000000);if(!weld.TryGetValue(q,out int index)){index=i;weld.Add(q,index);}canonical[i]=index;}
            var adjacency=new Dictionary<(int,int),List<Edge>>();
            int Add(int vertex,int bone)
            {
                var key=(vertex,bone);if(map.TryGetValue(key,out int result))return result;result=positions.Count;map.Add(key,result);positions.Add(vertices[vertex]);n.Add(normals.Length==vertices.Length?normals[vertex]:Vector3.up);u.Add(uv.Length==vertices.Length?uv[vertex]:Vector2.zero);weights.Add(new BoneWeight{boneIndex0=bone,weight0=1});return result;
            }
            for(int s=0;s<source.subMeshCount;s++)
            {
                sub.Add(new List<int>());var triangles=source.GetTriangles(s);
                for(int t=0;t<triangles.Length;t+=3)
                {
                    int bone=Dominant(sourceWeights,triangles[t],triangles[t+1],triangles[t+2]);
                    for(int e=0;e<3;e++)
                    {
                        int a=triangles[t+e],b=triangles[t+(e+1)%3];sub[s].Add(Add(a,bone));int ca=canonical[a],cb=canonical[b];if(ca==cb)continue;
                        var key=(Math.Min(ca,cb),Math.Max(ca,cb));if(!adjacency.TryGetValue(key,out var list)){list=new List<Edge>();adjacency.Add(key,list);}list.Add(new Edge(ca,cb,bone));
                    }
                }
            }
            // Cap only genuine new articulation boundaries, never remove/hide source triangles.
            var cuts=new Dictionary<int,List<Edge>>();foreach(var entry in adjacency.Values)
            {
                if(entry.Count!=2||entry[0].bone==entry[1].bone)continue;
                foreach(var edge in entry){if(!cuts.ContainsKey(edge.bone))cuts[edge.bone]=new List<Edge>();cuts[edge.bone].Add(edge);}
            }
            caps=0;unclosed=0;var interior=new List<int>();sub.Add(interior);
            foreach(var group in cuts)
            {
                var remaining=new List<Edge>(group.Value);
                while(remaining.Count>0)
                {
                    var first=remaining[0];remaining.RemoveAt(0);var loop=new List<int>{first.a,first.b};int guard=0;
                    while(loop.Last()!=loop[0]&&guard++<4096)
                    {
                        int index=remaining.FindIndex(e=>e.a==loop.Last());if(index<0)break;var edge=remaining[index];remaining.RemoveAt(index);loop.Add(edge.b);
                    }
                    if(loop.Last()!=loop[0]||loop.Count<4){unclosed++;continue;}loop.RemoveAt(loop.Count-1);
                    var center=Vector3.zero;foreach(int v in loop)center+=vertices[v];center/=loop.Count;
                    var normal=Vector3.zero;for(int i=0;i<loop.Count;i++)normal+=Vector3.Cross(vertices[loop[(i+1)%loop.Count]]-center,vertices[loop[i]]-center);normal.Normalize();
                    int start=positions.Count;positions.Add(center);n.Add(normal);u.Add(Vector2.one*.5f);weights.Add(new BoneWeight{boneIndex0=group.Key,weight0=1});
                    foreach(int v in loop){positions.Add(vertices[v]);n.Add(normal);u.Add(new Vector2(vertices[v].x*100,vertices[v].z*100));weights.Add(new BoneWeight{boneIndex0=group.Key,weight0=1});}
                    for(int i=0;i<loop.Count;i++)interior.AddRange(new[]{start,start+1+(i+1)%loop.Count,start+1+i});caps++;
                }
            }
            var result=new Mesh{name=name,indexFormat=positions.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};result.SetVertices(positions);result.SetNormals(n);result.SetUVs(0,u);result.boneWeights=weights.ToArray();result.bindposes=source.bindposes;result.subMeshCount=sub.Count;
            for(int s=0;s<sub.Count;s++)result.SetTriangles(sub[s],s);result.RecalculateBounds();result.RecalculateTangents();return result;
        }
        public static string Apply()
        {
            VisualOverhaulRepairApply.Guard();var log=new StringBuilder();string path="Assets/IronRequiem/Prefabs/Campaign/T07_Current_Repaired.prefab";var root=PrefabUtility.LoadPrefabContents(path);try
            {
                foreach(var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(s=>s.name.StartsWith("Body_LOD")||s.name.StartsWith("Arm_R_LOD")))
                {
                    if(AssetDatabase.GetAssetPath(skin.sharedMesh).StartsWith(VisualOverhaulRepair.Art))continue;
                    var mesh=Build(skin.sharedMesh,"T07_Rigid_"+skin.name,out int caps,out int unclosed);skin.sharedMesh=VisualOverhaulRepairApply.SaveMesh(mesh,mesh.name);
                    var mats=skin.sharedMaterials.ToList();mats.Add(AssetDatabase.LoadAssetAtPath<Material>("Assets/IronRequiem/Art/VisualOverhaul/Materials/VO_Suit_Joint.mat"));skin.sharedMaterials=mats.ToArray();
                    log.AppendLine(skin.name+" vertices="+mesh.vertexCount+" boneCount="+skin.bones.Length+" mixed=0 new joint caps="+caps+" incompleteBoundaryChains="+unclosed+" source triangles retained");
                }
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }finally{PrefabUtility.UnloadPrefabContents(root);}AssetDatabase.SaveAssets();File.WriteAllText(VisualOverhaulRepair.Report+"/rigid-applied.txt",log.ToString());return log.ToString();
        }
    }
}
