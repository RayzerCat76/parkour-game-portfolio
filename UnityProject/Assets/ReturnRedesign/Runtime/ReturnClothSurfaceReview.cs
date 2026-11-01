using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Return.Redesign
{
    // Explicit frozen runtime inspection only; original assets and ordinary play are untouched.
    public sealed class ReturnDreamerSurfaceReview
    {
        public bool Active {get;private set;}
        readonly Transform actor,water;readonly Camera camera;readonly string previewRoot;readonly float phase;
        readonly Dictionary<SkinnedMeshRenderer,Material[]> originals=new Dictionary<SkinnedMeshRenderer,Material[]>();
        readonly List<Material> materials=new List<Material>();
        readonly Dictionary<Transform,Vector3> positions=new Dictionary<Transform,Vector3>();
        readonly Dictionary<Transform,Quaternion> rotations=new Dictionary<Transform,Quaternion>();
        SkinnedMeshRenderer garment;Mesh originalMesh,diagnosticMesh;Material[] componentMaterials;bool waterActive,garmentEnabled;
        GameObject overlay;MeshRenderer overlayRenderer;int loadedTriangles,upperTrouserSamples;float upperTrouserMaximumExposure;
        Part[] parts=Array.Empty<Part>();
        [Serializable] public sealed class Part {public string name;public int vertices,triangles,upperWaistSamples;public float minimumHeight,maximumHeight,maximumUpperWaistExposure;public Color colour;}
        [Serializable] public sealed class Case {public string frame;public bool poseRetained,waterVisible;public Vector3 cameraPosition;public Quaternion cameraRotation;}
        [Serializable] public sealed class Report {public bool mechanicalPassed,meshRestored,materialsRestored,waterRestored,temporaryMeshReleased,temporaryRendererReleased,temporaryMaterialsReleased,upperTrouserImmersed;public string renderer,mesh;public int loadedVertices,loadedTriangles,upperTrouserSamples;public float swimPhase,upperTrouserMaximumExposure;public Part[] parts;public Case[] cases;public string boundary;}
        public ReturnDreamerSurfaceReview(Transform actor,Transform water,string previewRoot,Camera camera,float phase){this.actor=actor;this.water=water;this.previewRoot=previewRoot;this.camera=camera;this.phase=phase;}
        bool PoseRetained(){foreach(var p in positions)if(p.Key.localPosition!=p.Value)return false;foreach(var p in rotations){Quaternion a=p.Key.localRotation,b=p.Value;if(Mathf.Abs(a.x-b.x)+Mathf.Abs(a.y-b.y)+Mathf.Abs(a.z-b.z)+Mathf.Abs(a.w-b.w)>1e-6f)return false;}return true;}
        Material ColourMaterial(Material source,Color colour){var m=new Material(source);m.name="Temporary surface identity";m.SetTexture("_BaseMap",Texture2D.whiteTexture);m.SetColor("_BaseColor",colour);m.SetFloat("_ClothReview",1);m.SetFloat("_RecoveryChannel",0);m.SetFloat("_Skin",0);m.SetFloat("_Hair",0);m.SetFloat("_UseIndirect",0);materials.Add(m);return m;}
        void Prepare()
        {
            foreach(var r in actor.GetComponentsInChildren<SkinnedMeshRenderer>()){originals[r]=r.sharedMaterials;if(r.sharedMesh.name.Contains("male_casualsuit02"))garment=r;}
            if(garment==null)throw new InvalidOperationException("Actual garment renderer missing");
            originalMesh=garment.sharedMesh;garmentEnabled=garment.enabled;diagnosticMesh=new Mesh();garment.BakeMesh(diagnosticMesh,false);diagnosticMesh.name="Temporary frozen garment snapshot";
            Vector3[] vertices=diagnosticMesh.vertices;int[] triangles=diagnosticMesh.triangles;loadedTriangles=triangles.Length/3;if(vertices.Length!=originalMesh.vertexCount||triangles.Length==0)throw new InvalidOperationException("Baked garment geometry missing");int[] parent=new int[vertices.Length];for(int i=0;i<parent.Length;i++)parent[i]=i;
            int Find(int i){while(parent[i]!=i){parent[i]=parent[parent[i]];i=parent[i];}return i;}
            void Join(int a,int b){a=Find(a);b=Find(b);if(a!=b)parent[b]=a;}
            var coincident=new Dictionary<Vector3Int,int>();
            for(int i=0;i<vertices.Length;i++){Vector3 p=vertices[i]*100000;var key=new Vector3Int(Mathf.RoundToInt(p.x),Mathf.RoundToInt(p.y),Mathf.RoundToInt(p.z));if(coincident.TryGetValue(key,out int previous))Join(i,previous);else coincident.Add(key,i);}
            for(int i=0;i<triangles.Length;i+=3){Join(triangles[i],triangles[i+1]);Join(triangles[i],triangles[i+2]);}
            var groups=new Dictionary<int,List<int>>();for(int i=0;i<triangles.Length;i+=3){int key=Find(triangles[i]);if(!groups.TryGetValue(key,out var list)){list=new List<int>();groups.Add(key,list);}list.Add(triangles[i]);list.Add(triangles[i+1]);list.Add(triangles[i+2]);}
            var descriptions=new List<Part>();var applied=new List<Material>();diagnosticMesh.subMeshCount=groups.Count;int slot=0,shirt=0,pants=0;
            foreach(var group in groups.Values)
            {
                var unique=new HashSet<int>(group);float low=float.PositiveInfinity,high=float.NegativeInfinity;
                foreach(int i in unique){float y=actor.InverseTransformPoint(garment.transform.TransformPoint(vertices[i])).y;low=Mathf.Min(low,y);high=Mathf.Max(high,y);}
                string name=low>.5f&&high>1.3f?"shirt":low<.15f&&high<1.1f?"trousers":"unclassified";if(name=="shirt")shirt++;if(name=="trousers")pants++;
                int waistSamples=0;float maximumExposure=0;
                if(name=="trousers")
                {
                    maximumExposure=float.NegativeInfinity;float waterTime=Shader.GetGlobalFloat("_ReturnWaterTime");
                    foreach(int i in unique){Vector3 world=garment.transform.TransformPoint(vertices[i]);if(actor.InverseTransformPoint(world).y<=.8f)continue;waistSamples++;maximumExposure=Mathf.Max(maximumExposure,world.y-ReturnWaterSurface.Height(world.x,world.z,waterTime));}
                    if(waistSamples==0)throw new InvalidOperationException("Upper trouser samples missing");upperTrouserSamples=waistSamples;upperTrouserMaximumExposure=maximumExposure;
                }
                Color colour=name=="shirt"?Color.magenta:name=="trousers"?Color.cyan:Color.red;
                descriptions.Add(new Part{name=name,vertices=unique.Count,triangles=group.Count/3,minimumHeight=low,maximumHeight=high,colour=colour,upperWaistSamples=waistSamples,maximumUpperWaistExposure=maximumExposure});diagnosticMesh.SetTriangles(group,slot++,false);applied.Add(ColourMaterial(garment.sharedMaterials[0],colour));
            }
            parts=descriptions.ToArray();componentMaterials=applied.ToArray();if(shirt!=1||pants!=1||parts.Length!=2)throw new InvalidOperationException("Loaded garment components were not one shirt and one trouser component");
            overlay=new GameObject("Temporary frozen garment surface identity");overlay.transform.SetParent(garment.transform,false);overlay.AddComponent<MeshFilter>().sharedMesh=diagnosticMesh;overlayRenderer=overlay.AddComponent<MeshRenderer>();overlayRenderer.sharedMaterials=componentMaterials;overlay.SetActive(false);
        }
        void Restore(){if(garment!=null&&originalMesh!=null){garment.sharedMesh=originalMesh;garment.enabled=garmentEnabled;}if(overlay!=null)overlay.SetActive(false);foreach(var pair in originals)pair.Key.sharedMaterials=pair.Value;water.gameObject.SetActive(waterActive);}
        public IEnumerator Capture(Func<string,IEnumerator> capture,Func<bool> busy)
        {
            Active=true;waterActive=water.gameObject.activeSelf;var cases=new List<Case>();bool completed=false;
            foreach(var t in actor.GetComponentsInChildren<Transform>()){positions[t]=t.localPosition;rotations[t]=t.localRotation;}positions[camera.transform]=camera.transform.localPosition;rotations[camera.transform]=camera.transform.localRotation;
            string[] names={"Dreamer-Surface-Original","Dreamer-Surface-Components","Dreamer-Surface-NoWater","Dreamer-Surface-Restored"};
            try
            {
                Prepare();
                for(int index=0;index<names.Length;index++)
                {
                    Restore();if(index==1||index==2){garment.enabled=false;overlay.SetActive(true);foreach(var pair in originals)if(pair.Key!=garment){var slots=new Material[pair.Value.Length];for(int i=0;i<slots.Length;i++)slots[i]=ColourMaterial(originals[garment][0],Color.yellow);pair.Key.sharedMaterials=slots;}if(index==2)water.gameObject.SetActive(false);}
                    if(!PoseRetained())throw new InvalidOperationException("Surface diagnostic pose/camera moved");yield return null;yield return capture(names[index]);
                    string file=Path.Combine(previewRoot,"Evidence","Frames",names[index]+".png");float started=Time.realtimeSinceStartup;while(busy()||!File.Exists(file)){if(Time.realtimeSinceStartup-started>20)throw new TimeoutException("Surface capture "+names[index]);yield return null;}
                    cases.Add(new Case{frame=names[index],poseRetained=PoseRetained(),waterVisible=water.gameObject.activeSelf,cameraPosition=camera.transform.position,cameraRotation=camera.transform.rotation});
                }
                completed=true;
            }
            finally
            {
                Restore();bool slotsRestored=true;foreach(var pair in originals){var actual=pair.Key.sharedMaterials;for(int i=0;i<pair.Value.Length;i++)slotsRestored&=actual[i]==pair.Value[i];}bool meshRestored=garment!=null&&garment.sharedMesh==originalMesh&&garment.enabled==garmentEnabled,waterRestored=water.gameObject.activeSelf==waterActive;
                foreach(var material in materials)UnityEngine.Object.DestroyImmediate(material);if(overlay!=null)UnityEngine.Object.DestroyImmediate(overlay);if(diagnosticMesh!=null)UnityEngine.Object.DestroyImmediate(diagnosticMesh);bool meshReleased=diagnosticMesh==null,rendererReleased=overlay==null,materialsReleased=materials.TrueForAll(m=>m==null);
                bool immersed=upperTrouserSamples>0&&upperTrouserMaximumExposure<=-.005f;
                bool passed=completed&&cases.Count==4&&cases.TrueForAll(c=>c.poseRetained)&&meshRestored&&slotsRestored&&waterRestored&&meshReleased&&rendererReleased&&materialsReleased&&immersed;
                var report=new Report{mechanicalPassed=passed,meshRestored=meshRestored,materialsRestored=slotsRestored,waterRestored=waterRestored,temporaryMeshReleased=meshReleased,temporaryRendererReleased=rendererReleased,temporaryMaterialsReleased=materialsReleased,upperTrouserImmersed=immersed,upperTrouserSamples=upperTrouserSamples,upperTrouserMaximumExposure=upperTrouserMaximumExposure,renderer=garment==null?"":garment.name,mesh=originalMesh==null?"":originalMesh.name,loadedVertices=originalMesh==null?0:originalMesh.vertexCount,loadedTriangles=loadedTriangles,swimPhase=phase,parts=parts,cases=cases.ToArray(),boundary="Same frozen runtime pose/camera. Readable baked geometry identifies magenta shirt/cyan trousers/yellow other surfaces. Upper-trouser vertices above .8m in the actor frame are measured against the same wave function/time; this single-glide immersion gate does not cover every stroke or prove creative acceptance."};
                string directory=Environment.GetEnvironmentVariable("RETURN_EVIDENCE_DIR");if(string.IsNullOrEmpty(directory))directory=previewRoot;Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,"dreamer-surface-review.json"),JsonUtility.ToJson(report,true));
                Active=false;Debug.Log("[RETURN dreamer surface review] "+(passed?"PASS":"FAIL"));Application.Quit(passed?0:1);
            }
        }
    }
    // Runs only for the explicit native cloth review; ordinary play never constructs it.
    public sealed class ReturnClothSurfaceReview
    {
        public bool Active {get;private set;}
        readonly float poseTime;
        readonly string previewRoot;
        readonly Camera camera;
        readonly Dictionary<SkinnedMeshRenderer,Material[]> originals=new Dictionary<SkinnedMeshRenderer,Material[]>();
        readonly Dictionary<Material,Material> clones=new Dictionary<Material,Material>();
        readonly Dictionary<Light,LightShadows> lights=new Dictionary<Light,LightShadows>();
        readonly Dictionary<Transform,Vector3> positions=new Dictionary<Transform,Vector3>();
        readonly Dictionary<Transform,Quaternion> rotations=new Dictionary<Transform,Quaternion>();
        [Serializable] sealed class Case
        {
            public string frame;public float clothMode;public bool dynamicShadowsOff,poseRetained;
            public Vector3 cameraPosition;public Quaternion cameraRotation;
        }
        [Serializable] sealed class Report
        {
            public bool mechanicalPassed,materialsRestored,lightsRestored,sourceDefaultsZero;
            public float frozenHospitalTime;public int clothRenderers,clonedMaterials,sceneLights;
            public string meaning="Same frozen hospital pose, camera and geometry. NoShadows disables dynamic scene-light shadows only; Flat and Normals override nurse cloth forward output only. Postprocessing remains active. Final images establish the visual effect, not these control records.";
            public string[] frames;public Case[] cases;
        }
        [Serializable] sealed class ClothWeight {public int bone;public float weight;}
        [Serializable] sealed class ClothBone {public string name;public Matrix4x4 bindPose,localToWorld;}
        [Serializable] sealed class RestSubmesh {public int[] indices;}
        [Serializable] sealed class RestMesh
        {
            public string renderer,mesh;public bool importedReadable;public Vector3[] positions,normals;public Vector2[] uv;
            public Matrix4x4[] bindPoses;public string[] boneNames;public int[] weightOffsets;public ClothWeight[] weights;public RestSubmesh[] submeshes;
        }
        [Serializable] sealed class RestReport
        {
            public bool captured,finite,weightStreamsValid;public int clearedBorrowedViews;public RestMesh[] meshes;
        }
        [Serializable] sealed class StreamLengths
        {
            public string mesh;public bool runtimeReadable;public int sourceVertexCount,editorVertices,editorNormals,bakedVertices,bakedNormals;
        }
        [Serializable] sealed class StreamReport {public StreamLengths[] streams;}
        [Serializable] sealed class ClothTriangle
        {
            public int submesh,a,b,c;public float restArea,posedArea,areaRatio;
            public Vector3 restGeometricNormal,posedGeometricNormal;
        }
        [Serializable] sealed class ClothGeometry
        {
            public string renderer,mesh;public float transformDeterminant;
            public StreamLengths streams;
            public Matrix4x4 localToWorld;public Vector3[] restPositions,restNormals,posedPositions,posedNormals,geometricVertexNormals,screenPositions;
            public Vector2[] uv;public int[] weightOffsets;public ClothWeight[] weights;public ClothBone[] bones;
            public string[] blendShapes;public float[] blendShapeWeights;public ClothTriangle[] triangles;
        }
        [Serializable] sealed class GeometryReport
        {
            public bool captured,finite,weightStreamsValid,poseRetained;
            public float frozenHospitalTime;public int width,height,clearedBorrowedViews,temporaryMeshReleaseRequests,restSnapshotMeshes,restSnapshotClearedBorrowedViews;
            public Vector3 cameraPosition;public Quaternion cameraRotation;public Matrix4x4 worldToCamera,projection;
            public string coordinates="Positions and normals are world space; screen x/y are pixels from top left, z is camera depth. Geometric normals use triangle winding, including transform parity. Source rest surface uses renderer transform, without skinning. Flat weights use per-vertex weightOffsets. Rest streams are exported from the exact imported meshes outside play; runtime never reads inaccessible source streams. Editor source weight views are borrowed and only cleared.";
            public ClothGeometry[] renderers;
        }
        static bool Finite(float value){return !float.IsNaN(value)&&!float.IsInfinity(value);}
        static bool Finite(Vector3 value){return Finite(value.x)&&Finite(value.y)&&Finite(value.z);}
        static bool Finite(Matrix4x4 value){for(int i=0;i<16;i++)if(!Finite(value[i]))return false;return true;}
        public static void ExportRestGeometry(GameObject model,string file)
        {
            if(Application.isPlaying)throw new InvalidOperationException("Cloth rest export requires editor outside play");
            var result=new RestReport{finite=true,weightStreamsValid=true};var records=new List<RestMesh>();
            foreach(var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh=renderer.sharedMesh;if(mesh==null)continue;var bones=renderer.bones;
                var record=new RestMesh{renderer=renderer.name,mesh=mesh.name,importedReadable=mesh.isReadable,positions=mesh.vertices,normals=mesh.normals,uv=mesh.uv,bindPoses=mesh.bindposes,boneNames=new string[bones.Length],submeshes=new RestSubmesh[mesh.subMeshCount]};
                if(record.positions.Length!=mesh.vertexCount||record.normals.Length!=mesh.vertexCount||record.bindPoses.Length!=bones.Length)throw new InvalidOperationException("Editor cloth rest streams disagree: "+mesh.name);
                for(int vertex=0;vertex<record.positions.Length;vertex++)result.finite&=Finite(record.positions[vertex])&&Finite(record.normals[vertex]);
                for(int bone=0;bone<bones.Length;bone++){if(bones[bone]==null)throw new InvalidOperationException("Rest cloth bone missing");record.boneNames[bone]=bones[bone].name;result.finite&=Finite(record.bindPoses[bone]);}
                for(int slot=0;slot<mesh.subMeshCount;slot++){var indices=mesh.GetTriangles(slot);if(indices.Length%3!=0)throw new InvalidOperationException("Rest cloth triangle stream invalid");foreach(int index in indices)if(index<0||index>=mesh.vertexCount)throw new InvalidOperationException("Rest cloth index invalid");record.submeshes[slot]=new RestSubmesh{indices=indices};}
                var counts=mesh.GetBonesPerVertex();var weights=mesh.GetAllBoneWeights();
                try
                {
                    record.weightOffsets=new int[counts.Length+1];record.weights=new ClothWeight[weights.Length];
                    for(int vertex=0;vertex<counts.Length;vertex++)record.weightOffsets[vertex+1]=record.weightOffsets[vertex]+counts[vertex];
                    if(counts.Length!=mesh.vertexCount||record.weightOffsets[counts.Length]!=weights.Length)throw new InvalidOperationException("Rest cloth weight stream invalid");
                    for(int index=0;index<weights.Length;index++){var weight=weights[index];record.weights[index]=new ClothWeight{bone=weight.boneIndex,weight=weight.weight};result.finite&=Finite(weight.weight);result.weightStreamsValid&=weight.boneIndex>=0&&weight.boneIndex<bones.Length&&weight.weight>=0;}
                    for(int vertex=0;vertex<counts.Length;vertex++){float total=0;for(int index=record.weightOffsets[vertex];index<record.weightOffsets[vertex+1];index++)total+=weights[index].weight;result.weightStreamsValid&=Mathf.Abs(total-1)<.001f;}
                }
                finally{counts=default;weights=default;result.clearedBorrowedViews+=2;}
                records.Add(record);
            }
            result.meshes=records.ToArray();result.captured=records.Count>0&&result.finite&&result.weightStreamsValid;
            Directory.CreateDirectory(Path.GetDirectoryName(file));File.WriteAllText(file,JsonUtility.ToJson(result,false));
            if(!result.captured)throw new InvalidOperationException("Cloth editor rest export invalid");
        }
        void CaptureGeometry(GameObject nurse)
        {
            string directory=Environment.GetEnvironmentVariable("RETURN_EVIDENCE_DIR");if(string.IsNullOrEmpty(directory))directory=previewRoot;
            var snapshot=JsonUtility.FromJson<RestReport>(File.ReadAllText(Path.Combine(directory,"cloth-rest-geometry.json")));
            if(snapshot==null||!snapshot.captured||!snapshot.finite||!snapshot.weightStreamsValid)throw new InvalidOperationException("Cloth editor rest snapshot invalid");
            var result=new GeometryReport{finite=true,weightStreamsValid=true,frozenHospitalTime=poseTime,width=Screen.width,height=Screen.height,cameraPosition=camera.transform.position,cameraRotation=camera.transform.rotation,worldToCamera=camera.worldToCameraMatrix,projection=camera.projectionMatrix};
            result.restSnapshotMeshes=snapshot.meshes.Length;result.restSnapshotClearedBorrowedViews=snapshot.clearedBorrowedViews;
            var records=new List<ClothGeometry>();var streams=new List<StreamLengths>();
            foreach(var renderer in nurse.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var materials=renderer.sharedMaterials;var clothSlots=new HashSet<int>();
                for(int slot=0;slot<materials.Length;slot++)if(materials[slot]!=null&&materials[slot].name.StartsWith("Nurse_Clothes",StringComparison.Ordinal))clothSlots.Add(slot);
                if(clothSlots.Count==0)continue;
                var source=renderer.sharedMesh;if(source==null)throw new InvalidOperationException("Cloth source mesh missing");
                var matches=Array.FindAll(snapshot.meshes,m=>m.mesh==source.name&&m.positions.Length==source.vertexCount&&m.submeshes.Length==source.subMeshCount);
                if(matches.Length!=1)throw new InvalidOperationException("Rest cloth identity missing or ambiguous: "+source.name);
                var imported=matches[0];var bones=renderer.bones;
                if(imported.boneNames.Length!=bones.Length)throw new InvalidOperationException("Rest cloth bone identity count differs");
                for(int bone=0;bone<bones.Length;bone++)if(bones[bone]==null||bones[bone].name!=imported.boneNames[bone])throw new InvalidOperationException("Rest cloth bone identity differs");
                var baked=new Mesh();
                try
                {
                    renderer.BakeMesh(baked);
                    var rest=imported.positions;var restNormals=imported.normals;var posed=baked.vertices;var normals=baked.normals;
                    var lengths=new StreamLengths{mesh=source.name,runtimeReadable=source.isReadable,sourceVertexCount=source.vertexCount,editorVertices=rest.Length,editorNormals=restNormals.Length,bakedVertices=posed.Length,bakedNormals=normals.Length};streams.Add(lengths);
                    File.WriteAllText(Path.Combine(directory,"cloth-stream-lengths.json"),JsonUtility.ToJson(new StreamReport{streams=streams.ToArray()},true));
                    if(rest.Length!=posed.Length||normals.Length!=posed.Length||restNormals.Length!=rest.Length)throw new InvalidOperationException("Cloth geometry streams disagree: "+JsonUtility.ToJson(lengths));
                    var transform=renderer.localToWorldMatrix;var normalTransform=transform.inverse.transpose;
                    var record=new ClothGeometry{renderer=renderer.name,mesh=source.name,streams=lengths,localToWorld=transform,transformDeterminant=transform.determinant,restPositions=new Vector3[rest.Length],restNormals=new Vector3[rest.Length],posedPositions=new Vector3[rest.Length],posedNormals=new Vector3[rest.Length],geometricVertexNormals=new Vector3[rest.Length],screenPositions=new Vector3[rest.Length],uv=imported.uv};
                    result.finite&=Finite(transform)&&Finite(record.transformDeterminant);
                    for(int vertex=0;vertex<rest.Length;vertex++)
                    {
                        record.restPositions[vertex]=transform.MultiplyPoint3x4(rest[vertex]);record.restNormals[vertex]=normalTransform.MultiplyVector(restNormals[vertex]).normalized;
                        record.posedPositions[vertex]=transform.MultiplyPoint3x4(posed[vertex]);record.posedNormals[vertex]=normalTransform.MultiplyVector(normals[vertex]).normalized;
                        var screen=camera.WorldToScreenPoint(record.posedPositions[vertex]);screen.y=Screen.height-screen.y;record.screenPositions[vertex]=screen;
                        result.finite&=Finite(record.restPositions[vertex])&&Finite(record.restNormals[vertex])&&Finite(record.posedPositions[vertex])&&Finite(record.posedNormals[vertex])&&Finite(screen);
                    }
                    var triangles=new List<ClothTriangle>();
                    for(int submesh=0;submesh<imported.submeshes.Length;submesh++)
                    {
                        if(!clothSlots.Contains(submesh))continue;var indices=imported.submeshes[submesh].indices;
                        for(int index=0;index<indices.Length;index+=3)
                        {
                            int a=indices[index],b=indices[index+1],c=indices[index+2];
                            var restCross=Vector3.Cross(record.restPositions[b]-record.restPositions[a],record.restPositions[c]-record.restPositions[a]);
                            var posedCross=Vector3.Cross(record.posedPositions[b]-record.posedPositions[a],record.posedPositions[c]-record.posedPositions[a]);
                            float restArea=restCross.magnitude*.5f,posedArea=posedCross.magnitude*.5f;
                            var triangle=new ClothTriangle{submesh=submesh,a=a,b=b,c=c,restArea=restArea,posedArea=posedArea,areaRatio=restArea>1e-12f?posedArea/restArea:0,restGeometricNormal=restCross.normalized,posedGeometricNormal=posedCross.normalized};
                            triangles.Add(triangle);record.geometricVertexNormals[a]+=posedCross;record.geometricVertexNormals[b]+=posedCross;record.geometricVertexNormals[c]+=posedCross;
                            result.finite&=Finite(restCross)&&Finite(posedCross)&&Finite(triangle.areaRatio);
                        }
                    }
                    for(int vertex=0;vertex<rest.Length;vertex++)record.geometricVertexNormals[vertex]=record.geometricVertexNormals[vertex].normalized;
                    record.triangles=triangles.ToArray();var binds=imported.bindPoses;
                    record.bones=new ClothBone[bones.Length];
                    if(binds.Length!=bones.Length)throw new InvalidOperationException("Cloth bone/bind streams disagree");
                    for(int bone=0;bone<bones.Length;bone++)
                    {
                        if(bones[bone]==null)throw new InvalidOperationException("Cloth bone missing");
                        record.bones[bone]=new ClothBone{name=bones[bone].name,bindPose=binds[bone],localToWorld=bones[bone].localToWorldMatrix};
                        result.finite&=Finite(binds[bone])&&Finite(record.bones[bone].localToWorld);
                    }
                    record.weightOffsets=imported.weightOffsets;record.weights=imported.weights;
                    result.weightStreamsValid&=record.weightOffsets.Length==rest.Length+1&&record.weightOffsets[rest.Length]==record.weights.Length;
                    for(int index=0;index<record.weights.Length;index++)
                    {
                        var weight=record.weights[index];result.finite&=Finite(weight.weight);result.weightStreamsValid&=weight.bone>=0&&weight.bone<bones.Length&&weight.weight>=0;
                    }
                    for(int vertex=0;vertex<rest.Length;vertex++){float total=0;for(int index=record.weightOffsets[vertex];index<record.weightOffsets[vertex+1];index++)total+=record.weights[index].weight;result.weightStreamsValid&=Mathf.Abs(total-1)<.001f;}
                    record.blendShapes=new string[source.blendShapeCount];record.blendShapeWeights=new float[source.blendShapeCount];
                    for(int shape=0;shape<source.blendShapeCount;shape++){record.blendShapes[shape]=source.GetBlendShapeName(shape);record.blendShapeWeights[shape]=renderer.GetBlendShapeWeight(shape);result.finite&=Finite(record.blendShapeWeights[shape]);}
                    records.Add(record);
                }
                finally{UnityEngine.Object.Destroy(baked);result.temporaryMeshReleaseRequests++;}
            }
            result.renderers=records.ToArray();result.poseRetained=PoseRetained();result.captured=records.Count>0&&result.finite&&result.weightStreamsValid&&result.poseRetained;
            Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,"cloth-posed-geometry.json"),JsonUtility.ToJson(result,false));
            if(!result.captured)throw new InvalidOperationException("Posed cloth geometry invalid");
        }
        public ReturnClothSurfaceReview(float time,string root,Camera view){poseTime=time;previewRoot=root;camera=view;Active=true;}
        bool PoseRetained()
        {
            foreach(var pair in positions)if(pair.Key.localPosition!=pair.Value||pair.Key.localRotation!=rotations[pair.Key])return false;
            return true;
        }
        void RestoreLights(){foreach(var pair in lights)if(pair.Key!=null)pair.Key.shadows=pair.Value;}
        public IEnumerator Capture(Func<string,IEnumerator> capture,Func<bool> busy)
        {
            string[] frames={"Hospital-Cloth-Original","Hospital-Cloth-NoShadows","Hospital-Cloth-Flat","Hospital-Cloth-Normals","Hospital-Cloth-Restored"};
            var cases=new List<Case>();bool completed=false,defaultsZero=true;
            try
            {
                var nurse=GameObject.Find("The nurse");if(nurse==null)throw new InvalidOperationException("Nurse missing from cloth review");
                foreach(var t in nurse.GetComponentsInChildren<Transform>()){positions[t]=t.localPosition;rotations[t]=t.localRotation;}
                positions[camera.transform]=camera.transform.localPosition;rotations[camera.transform]=camera.transform.localRotation;
                CaptureGeometry(nurse);
                foreach(var renderer in nurse.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    var slots=renderer.sharedMaterials;var applied=(Material[])slots.Clone();bool changed=false;
                    for(int i=0;i<slots.Length;i++)
                    {
                        var original=slots[i];if(original==null||!original.name.StartsWith("Nurse_Clothes",StringComparison.Ordinal))continue;
                        if(!original.HasProperty("_ClothReview"))throw new InvalidOperationException("Cloth review shader control missing");
                        defaultsZero&=original.GetFloat("_ClothReview")==0;
                        if(!clones.TryGetValue(original,out Material clone)){clone=new Material(original);clone.name=original.name+" cloth review";clones.Add(original,clone);}
                        applied[i]=clone;changed=true;
                    }
                    if(changed){originals.Add(renderer,slots);renderer.sharedMaterials=applied;}
                }
                if(originals.Count==0||!defaultsZero)throw new InvalidOperationException("Cloth review inputs invalid");
                foreach(var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))lights[light]=light.shadows;
                for(int index=0;index<frames.Length;index++)
                {
                    RestoreLights();if(index==1)foreach(var pair in lights)pair.Key.shadows=LightShadows.None;
                    float mode=index==2?1:index==3?2:0;foreach(var pair in clones)pair.Value.SetFloat("_ClothReview",mode);
                    if(!PoseRetained())throw new InvalidOperationException("Cloth diagnostic pose moved");
                    yield return null;yield return capture(frames[index]);
                    string file=Path.Combine(previewRoot,"Evidence","Frames",frames[index]+".png");float started=Time.realtimeSinceStartup;
                    while(busy()||!File.Exists(file))
                    {
                        if(Time.realtimeSinceStartup-started>20)throw new TimeoutException("Cloth diagnostic capture: "+frames[index]);
                        yield return null;
                    }
                    cases.Add(new Case{frame=frames[index],clothMode=mode,dynamicShadowsOff=index==1,poseRetained=PoseRetained(),cameraPosition=camera.transform.position,cameraRotation=camera.transform.rotation});
                }
                completed=true;
            }
            finally
            {
                RestoreLights();bool materialsRestored=true,lightsRestored=true;
                foreach(var pair in originals){pair.Key.sharedMaterials=pair.Value;var slots=pair.Key.sharedMaterials;for(int i=0;i<slots.Length;i++)materialsRestored&=slots[i]==pair.Value[i];}
                foreach(var pair in lights)lightsRestored&=pair.Key.shadows==pair.Value;
                foreach(var pair in clones)UnityEngine.Object.Destroy(pair.Value);
                bool passed=completed&&materialsRestored&&lightsRestored&&defaultsZero&&cases.TrueForAll(c=>c.poseRetained);
                var report=new Report{mechanicalPassed=passed,materialsRestored=materialsRestored,lightsRestored=lightsRestored,sourceDefaultsZero=defaultsZero,frozenHospitalTime=poseTime,clothRenderers=originals.Count,clonedMaterials=clones.Count,sceneLights=lights.Count,frames=frames,cases=cases.ToArray()};
                string directory=Environment.GetEnvironmentVariable("RETURN_EVIDENCE_DIR");if(string.IsNullOrEmpty(directory))directory=previewRoot;
                Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,"cloth-lighting-review.json"),JsonUtility.ToJson(report,true));
                Active=false;Debug.Log("[RETURN cloth review] "+(passed?"PASS":"FAIL")+" frozen pose "+poseTime.ToString("F4"));if(!passed)Application.Quit(1);
            }
        }
    }
}
