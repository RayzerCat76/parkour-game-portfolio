using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;

namespace Return.Redesign.Editor
{
    public static class ReturnBuild
    {
        public const string ScenePath="Assets/ReturnRedesign/Return.unity";
        [MenuItem("Return/Rebuild original-design scene")]
        public static void Scene()
        {
            ReturnRealismAssets.Prepare();
            ReturnConstructionAssets.Prepare();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            GameObject game=new GameObject("RETURN · original-design rebuild");game.AddComponent<ReturnGame>();
            // This serialized material keeps the custom ink shader in the standalone.
            Shader shader=Shader.Find("Return/Ink");
            if(shader==null)throw new BuildFailedException("Return ink shader is missing.");
            const string materialPath="Assets/ReturnRedesign/Ink.mat";
            Material material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,materialPath);}
            var reference=game.AddComponent<ReturnShaderReference>();reference.ink=material;
            const string resources="Assets/ReturnRedesign/Art/Resources";
            reference.volume=KeepShader("Return/Volume",resources+"/ReturnVolume.mat");
            reference.water=KeepShader("Return/Water",resources+"/ReturnWater.mat");
            reference.focus=KeepShader("Return/Focus",resources+"/ReturnFocus.mat");
            KeepShader("Return/DepthProbe",resources+"/ReturnDepthProbe.mat");
            KeepShader("Return/SkinDiffusion",resources+"/ReturnSkinDiffusion.mat");
            reference.volumePipeline=VolumePipeline(resources);
            EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();
            Debug.Log("[RETURN] New scene saved. Original scenes unchanged.");
        }
        static Material KeepShader(string name,string path)
        {
            Shader shader=Shader.Find(name);if(shader==null)throw new BuildFailedException(name+" shader missing");
            Material material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,path);}return material;
        }
        static UniversalRenderPipelineAsset VolumePipeline(string resources)
        {
            string path=resources+"/ReturnVolumePipeline.asset";
            var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if(pipeline!=null){ConfigureVolume(pipeline);return pipeline;}
            var renderer=ScriptableObject.CreateInstance<UniversalRendererData>();renderer.name="RETURN · forward renderer";
            ResourceReloader.ReloadAllNullIn(renderer,UniversalRenderPipelineAsset.packagePath);
            AssetDatabase.CreateAsset(renderer,resources+"/ReturnVolumeRenderer.asset");
            pipeline=UniversalRenderPipelineAsset.Create(renderer);pipeline.name="RETURN · volume pipeline";pipeline.supportsCameraDepthTexture=true;pipeline.supportsHDR=true;pipeline.shadowDistance=70;
            AssetDatabase.CreateAsset(pipeline,path);ConfigureVolume(pipeline);return pipeline;
        }
        static void ConfigureVolume(UniversalRenderPipelineAsset pipeline) {
            pipeline.supportsCameraOpaqueTexture=true;pipeline.supportsCameraDepthTexture=true;pipeline.supportsHDR=true;pipeline.msaaSampleCount=4;pipeline.mainLightShadowmapResolution=2048;pipeline.shadowCascadeCount=4;var ps=new SerializedObject(pipeline);ps.FindProperty("m_MainLightShadowsSupported").boolValue=true;ps.FindProperty("m_AdditionalLightsRenderingMode").intValue=1;ps.FindProperty("m_AdditionalLightShadowsSupported").boolValue=true;ps.FindProperty("m_AdditionalLightsShadowmapResolution").intValue=1024;ps.ApplyModifiedPropertiesWithoutUndo();
            var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/ReturnRedesign/Art/Resources/ReturnVolumeRenderer.asset");
            for(int i=renderer.rendererFeatures.Count-1;i>=0;i--)if(renderer.rendererFeatures[i] is ScreenSpaceAmbientOcclusion){var unavailable=renderer.rendererFeatures[i];renderer.rendererFeatures.RemoveAt(i);UnityEngine.Object.DestroyImmediate(unavailable,true);}
            if(!renderer.rendererFeatures.Exists(f=>f is ReturnDepthCaptureFeature)){var capture=ScriptableObject.CreateInstance<ReturnDepthCaptureFeature>();capture.name="RETURN hospital depth evidence";AssetDatabase.AddObjectToAsset(capture,renderer);renderer.rendererFeatures.Add(capture);}
            if(!renderer.rendererFeatures.Exists(f=>f is ReturnSkinDiffusionFeature)){var skin=ScriptableObject.CreateInstance<ReturnSkinDiffusionFeature>();skin.name="RETURN diffuse skin light";AssetDatabase.AddObjectToAsset(skin,renderer);renderer.rendererFeatures.Add(skin);}
            renderer.SetDirty();EditorUtility.SetDirty(renderer);
            EditorUtility.SetDirty(pipeline);
        }
        [MenuItem("Return/Diagnose leaf contact")]
        public static void DiagnoseLeafContact()
        {
            Scene();
            var importer=(UnityEditor.ModelImporter)UnityEditor.AssetImporter.GetAtPath("Assets/ReturnRedesign/Art/Resources/ReturnRealism/Dreamer.fbx");
            if(importer==null)throw new BuildFailedException("Dreamer importer missing for contact diagnosis");
            bool readable=importer.isReadable;ReturnJourney3D journey=null;GameObject cameraObject=null;
            try {
                importer.isReadable=true;importer.SaveAndReimport();
                cameraObject=new GameObject("Resting contact diagnosis camera");journey=new ReturnJourney3D(cameraObject.AddComponent<Camera>());journey.Begin();
                for(int i=0;i<120&&!journey.CanRest;i++)journey.Step(1f/60,new ReturnInput());
                if(!journey.CanRest)throw new BuildFailedException("Contact diagnosis did not reach the leaf");
                journey.Step(1f/60,new ReturnInput{interact=true});for(int i=0;i<240;i++)journey.Step(1f/60,new ReturnInput());
                var point=journey.DescribeRestContact();string directory=System.IO.Path.Combine(System.IO.Directory.GetParent(Application.dataPath).Parent.FullName,"Redesign","Evidence");
                System.IO.Directory.CreateDirectory(directory);System.IO.File.WriteAllText(System.IO.Path.Combine(directory,"rest-contact-diagnosis-v20.json"),JsonUtility.ToJson(point,true));
                Debug.Log("[RETURN] Rest contact diagnosis recorded: "+point.renderer+" vertex "+point.vertex+", gap "+point.clearance.ToString("F6")+", weights readable "+point.weightsReadable);
            } finally {if(journey!=null)journey.Dispose();if(cameraObject!=null)UnityEngine.Object.DestroyImmediate(cameraObject);importer.isReadable=readable;importer.SaveAndReimport();}
        }
        [System.Serializable] sealed class TransitionContact {public string stage,renderer;public int vertex;public float amount,clearance;public bool passed;}
        [MenuItem("Return/Diagnose seated leg contact")]
        public static void DiagnoseRestTransitions()
        {
            Scene();string modelPath="Assets/ReturnRedesign/Art/Resources/ReturnRealism/Dreamer.fbx";
            var importer=AssetImporter.GetAtPath(modelPath) as ModelImporter;if(importer==null)throw new BuildFailedException("Dreamer importer missing for transition diagnosis");
            bool readable=importer.isReadable;ReturnJourney3D journey=null;GameObject cameraObject=null;
            try {
                importer.isReadable=true;importer.SaveAndReimport();cameraObject=new GameObject("Transition contact diagnosis camera");journey=new ReturnJourney3D(cameraObject.AddComponent<Camera>());journey.Begin();
                for(int i=0;i<120&&!journey.CanRest;i++)journey.Step(1f/60,new ReturnInput());if(!journey.CanRest)throw new BuildFailedException("Transition diagnosis did not land on the leaf");
                var traveller=GameObject.Find("The dreamer").transform;var leaf=(Transform)typeof(ReturnJourney3D).GetField("leaf",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(journey);
                System.Func<Vector3,float> surface=p=>{Vector3 q=leaf.InverseTransformPoint(new Vector3(p.x,leaf.position.y,p.z));return leaf.TransformPoint(new Vector3(q.x,ReturnVolumeArt.LeafHeight(q.x,q.z),q.z)).y;};
                var results=new List<TransitionContact>();
                System.Action<string> record=stage=>{
                    var result=new TransitionContact{stage=stage,amount=journey.RestPoseAmount,clearance=float.PositiveInfinity};
                    foreach(var renderer in traveller.GetComponentsInChildren<SkinnedMeshRenderer>()){
                        var mesh=new Mesh();try{renderer.BakeMesh(mesh,false);var points=mesh.vertices;var weights=renderer.sharedMesh.boneWeights;var bones=renderer.bones;
                            for(int i=0;i<points.Length;i++){BoneWeight w=weights[i];int[] ids={w.boneIndex0,w.boneIndex1,w.boneIndex2,w.boneIndex3};float[] values={w.weight0,w.weight1,w.weight2,w.weight3};bool leg=false;
                                for(int j=0;j<4;j++){if(values[j]<.2f)continue;string name=bones[ids[j]].name;if(name.StartsWith("thigh_")||name.StartsWith("calf_")||name.StartsWith("foot_")||name.StartsWith("ball_")||name.StartsWith("toe_"))leg=true;}
                                if(!leg)continue;Vector3 world=renderer.transform.TransformPoint(points[i]);float gap=world.y-surface(world);if(gap<result.clearance){result.clearance=gap;result.renderer=renderer.name;result.vertex=i;}
                            }
                        }finally{UnityEngine.Object.DestroyImmediate(mesh);}
                    }
                    result.passed=!float.IsInfinity(result.clearance)&&result.clearance>=-.015f&&result.clearance<=.035f;results.Add(result);string output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../Redesign/Evidence/rest-transition-contact-v22.json"));Directory.CreateDirectory(Path.GetDirectoryName(output));File.WriteAllText(output,"[\n"+string.Join(",\n",results.ConvertAll(x=>JsonUtility.ToJson(x,true)))+"\n]\n");
                    Debug.Log("[RETURN] Transition leg contact "+(result.passed?"PASS":"FAIL")+": "+stage+", amount "+result.amount.ToString("F3")+", clearance "+result.clearance.ToString("F4")+", "+result.renderer+" vertex "+result.vertex);
                    if(!result.passed)throw new BuildFailedException("Transition leg contact outside [-0.015, 0.035] m: "+stage+" "+result.clearance.ToString("F4")+" m");
                };
                journey.Step(1f/60,new ReturnInput{interact=true});while(journey.RestPoseAmount<.45f)journey.Step(1f/60,new ReturnInput());record("sitting");
                while(journey.RestPoseAmount<.77f)journey.Step(1f/60,new ReturnInput());record("reclining");
                while(journey.RestPoseAmount<1)journey.Step(1f/60,new ReturnInput());journey.Step(1f/60,new ReturnInput{vertical=1});while(journey.RestPoseAmount>.45f)journey.Step(1f/60,new ReturnInput());record("rising");
            }finally{if(journey!=null)journey.Dispose();if(cameraObject!=null)UnityEngine.Object.DestroyImmediate(cameraObject);importer.isReadable=readable;importer.SaveAndReimport();}
        }
        static float TriangleSurface(Vector3 point,Vector3[] vertices,int[] indices)
        {
            for(int i=0;i<indices.Length;i+=3){Vector3 a=vertices[indices[i]],b=vertices[indices[i+1]],c=vertices[indices[i+2]];float bx=b.x-a.x,bz=b.z-a.z,cx=c.x-a.x,cz=c.z-a.z,det=bx*cz-bz*cx;if(Mathf.Abs(det)<.000001f)continue;float x=point.x-a.x,z=point.z-a.z,u=(x*cz-z*cx)/det,v=(bx*z-bz*x)/det;if(u>=-.00001f&&v>=-.00001f&&u+v<=1.00001f)return a.y+u*(b.y-a.y)+v*(c.y-a.y);}
            throw new BuildFailedException("No rendered island triangle below validation point "+point);
        }
        static void ValidateIsland(List<string> checks)
        {
            var root=new GameObject("Natural island validation").transform;var art=new ReturnVolumeArt();
            const string grassPath="Assets/ReturnRedesign/Art/Resources/ReturnRealism/IslandGrass.fbx";
            var importer=AssetImporter.GetAtPath(grassPath) as ModelImporter;if(importer==null)throw new BuildFailedException("Photographed grass importer missing");bool readable=importer.isReadable;
            try{
                var island=art.Island(root,Vector3.zero,art.Material(Color.gray));Mesh mesh=island.GetComponent<MeshFilter>().sharedMesh;var vertices=mesh.vertices;var indices=mesh.triangles;float maxError=0,minimumRadius=float.PositiveInfinity,maximumRadius=0;
                for(int i=0;i<32;i++){float angle=i*Mathf.PI*2/32,radius=ReturnVolumeArt.IslandRadius(angle);minimumRadius=Mathf.Min(minimumRadius,radius);maximumRadius=Mathf.Max(maximumRadius,radius);for(int ring=1;ring<=3;ring++){Vector3 point=new Vector3(Mathf.Cos(angle)*(radius-.3f)*ring/3,0,Mathf.Sin(angle)*(radius-.3f)*ring/3);maxError=Mathf.Max(maxError,Mathf.Abs(TriangleSurface(point,vertices,indices)-ReturnVolumeArt.IslandHeight(point.x,point.z)));}if(ReturnVolumeArt.IslandHeight(Mathf.Cos(angle)*(radius+.5f),Mathf.Sin(angle)*(radius+.5f))>-.40f)throw new BuildFailedException("Island outer bank is exposed above the white liquid");}
                if(maxError>.012f||maximumRadius-minimumRadius<.35f)throw new BuildFailedException("Island rendered/support shape differs: error "+maxError+", radius range "+(maximumRadius-minimumRadius));
                checks.Add("irregular shoreline submerges its bank; 96 rendered-triangle/support samples agree within "+maxError.ToString("F4")+" m");
                importer.isReadable=true;importer.SaveAndReimport();var grass=art.Model("IslandGrass",root,Vector3.zero);grass.localRotation=Quaternion.Euler(0,180,0)*grass.localRotation;int roots=0;float maxRootError=0;
                foreach(var filter in grass.GetComponentsInChildren<MeshFilter>()){var points=filter.sharedMesh.vertices;var colours=filter.sharedMesh.colors32;if(points.Length!=colours.Length)throw new BuildFailedException("Grass root wind pins missing");for(int i=0;i<points.Length;i++)if(colours[i].a==0){Vector3 point=filter.transform.TransformPoint(points[i]);maxRootError=Mathf.Max(maxRootError,Mathf.Abs(point.y-TriangleSurface(point,vertices,indices)-.004f));roots++;}}
                if(roots<200||maxRootError>.012f)throw new BuildFailedException("Photographed grass roots differ from rendered ground: "+roots+" roots, "+maxRootError.ToString("F4")+" m");
                checks.Add("exported photographed grass roots follow the actual rendered triangles: "+roots+" roots, maximum error "+maxRootError.ToString("F4")+" m");
                var leaf=art.Leaf(root,art.Material(Color.green),art.Material(Color.green));leaf.localPosition=new Vector3(0,-.2f,ReturnVolumeArt.LeafDockZ);Mesh leafMesh=leaf.GetComponent<MeshFilter>().sharedMesh;var shape=leafMesh.vertices;var posed=new Vector3[shape.Length];var pins=new Color32[shape.Length];int leafContacts=0;float minimumLeaf=float.PositiveInfinity,maximumLift=0;
                foreach(float roll in new[]{-.7f,0,.7f}){leaf.localRotation=Quaternion.Euler(0,0,roll);ReturnVolumeArt.DrapeLeaf(leaf,leafMesh,shape,posed,pins);var actual=leafMesh.vertices;for(int i=0;i<shape.Length/2;i++){if(shape[i].z<=1.35f&&Vector3.Distance(shape[i],actual[i])>.000001f)throw new BuildFailedException("Shore drape moved the resting area");Vector3 world=leaf.TransformPoint(actual[i]);float lift=leaf.TransformPoint(actual[i]-shape[i]).y-leaf.position.y;maximumLift=Mathf.Max(maximumLift,lift);if(lift>.001f&&shape[i].z>=1.5f){float clearance=world.y-TriangleSurface(new Vector3(world.x,0,world.z-26),vertices,indices);minimumLeaf=Mathf.Min(minimumLeaf,clearance);leafContacts++;if(clearance<.004f||clearance>.015f||pins[i].a!=0)throw new BuildFailedException("Leaf cuts through the rendered bank or its attached tip moves in wind: "+clearance);if(Mathf.Abs(Vector3.Distance(actual[i],actual[i+shape.Length/2])-.004f)>.00001f)throw new BuildFailedException("Shore drape altered leaf thickness");}}}
                if(leafContacts<100||maximumLift>.6f)throw new BuildFailedException("Leaf did not rest onto the bank coherently: "+leafContacts+" contacts, "+maximumLift+" m lift");
                checks.Add("leaf drapes above actual shore triangles through three rolls, preserves its resting area/thickness and pins contact wind: "+leafContacts+" samples, minimum gap "+minimumLeaf.ToString("F4")+" m");
                var person=new ReturnPerson3D(art,root,"Terrain stance validation",null,null,null,true,0);var positions=new[]{new Vector2(.15f,-3.1f),new Vector2(.9f,-2.1f),new Vector2(1,-.8f),new Vector2(-1.6f,.3f),new Vector2(2.7f,.4f),new Vector2(-2.65f,-.9f)};float minGap=float.PositiveInfinity,maxGap=float.NegativeInfinity,time=0;
                Func<Vector3,float> surface=point=>ReturnVolumeArt.IslandHeight(point.x,point.z);
                foreach(var position in positions)for(int heading=0;heading<4;heading++){Vector3 point=new Vector3(position.x,surface(new Vector3(position.x,0,position.y))+person.StandingHeightOffset,position.y);for(int frame=0;frame<45;frame++)person.Pose(point,Vector3.zero,false,false,time+=1f/60,facing:heading*90,groundSurface:surface);Vector2 gaps=person.MeasureShoeClearance(surface);minGap=Mathf.Min(minGap,gaps.x,gaps.y);maxGap=Mathf.Max(maxGap,gaps.x,gaps.y);if(gaps.x<-.015f||gaps.y<-.015f||gaps.x>.035f||gaps.y>.035f||float.IsNaN(gaps.x)||float.IsNaN(gaps.y))throw new BuildFailedException("Terrain stance shoe contact failed at "+position+", heading "+heading+": "+gaps);}
                checks.Add("both actual shoe meshes contact uneven terrain across six locations and four headings: clearance "+minGap.ToString("F4")+" to "+maxGap.ToString("F4")+" m");
            }finally{UnityEngine.Object.DestroyImmediate(root.gameObject);art.Dispose();importer.isReadable=readable;importer.SaveAndReimport();}
        }
        [MenuItem("Return/Validate motor and source interactions")]
        public static void Validate()
        {
            List<string> checks=new List<string>();
            ReturnSurface ground=new ReturnSurface(0,0,20);List<ReturnSurface> surfaces=new List<ReturnSurface>{ground};
            ReturnMotor motor=new ReturnMotor();motor.Reset(Vector2.zero);motor.Step(1f/60,0,0,false,false,surfaces);
            if(!motor.Grounded)throw new BuildFailedException("Grounded spawn failed");checks.Add("grounded spawn");
            float apex=0;motor.Step(1f/60,0,0,true,true,surfaces);
            for(int i=0;i<90;i++){motor.Step(1f/60,0,0,false,true,surfaces);apex=Mathf.Max(apex,motor.position.y);}
            if(apex<2.4f||apex>3||!motor.Grounded)throw new BuildFailedException("Full jump / landing failed");checks.Add("full jump and swept landing");
            motor.Step(1f/60,0,0,true,true,surfaces);float shortApex=0;
            for(int i=0;i<90;i++){motor.Step(1f/60,0,0,false,false,surfaces);shortApex=Mathf.Max(shortApex,motor.position.y);}
            if(shortApex>=apex-.4f)throw new BuildFailedException("Variable jump height failed");checks.Add("variable jump height");
            motor.Reset(new Vector2(0,4));motor.velocity=new Vector2(0,-100);motor.Step(.08f,0,0,false,false,surfaces);
            if(!motor.Grounded||Mathf.Abs(motor.position.y)>.001f)throw new BuildFailedException("Fast landing tunnelled");checks.Add("fast fall does not tunnel");
            motor.Reset(new Vector2(0,4));motor.Step(1f/60,1,0,false,true,surfaces,true);
            if(!motor.Bursting||motor.BurstReady)throw new BuildFailedException("Burst did not consume its charge");
            float burstStart=motor.position.x;
            for(int i=0;i<10;i++)motor.Step(1f/60,1,0,false,true,surfaces,false);
            if(motor.position.x<burstStart+2||motor.BurstReady)throw new BuildFailedException("Burst movement / airborne charge failed");
            for(int i=0;i<100&&!motor.Grounded;i++)motor.Step(1f/60,0,0,false,true,surfaces,false);
            if(!motor.Grounded||!motor.BurstReady)throw new BuildFailedException("Landing failed to renew burst");
            checks.Add("burst carries momentum, consumes its charge and renews on landing");
            var springGround=new List<ReturnSurface>{new ReturnSurface(0,0,4,SurfaceKind.Spring)};
            motor.Reset(Vector2.zero);motor.Step(1f/60,0,0,false,true,springGround);float springApex=0;
            for(int i=0;i<60;i++){motor.Step(1f/60,0,0,false,true,springGround);springApex=Mathf.Max(springApex,motor.position.y);}
            if(springApex<5.5f)throw new BuildFailedException("Spring cannot reach the authored stairwell landing");
            checks.Add("spring arc clears the tallest authored spring ascent");
            ReturnArt art=new ReturnArt();ReturnWorld world=new ReturnWorld(art);
            for(int route=0;route<4;route++)
            {
                world.Build(Movement.Fragments,route,route);
                if(world.fragment==null||world.beacon==null||world.surfaces.Count<5)throw new BuildFailedException("Fragment route incomplete");
                int before=world.surfaces.Count;ReturnSurface outward=world.surfaces[1];world.OpenReturn();
                if(world.surfaces.Count<=before+4||outward.solid||world.surfaces.FindAll(s=>s.returnPath).Count<5)throw new BuildFailedException("Separate return circuit missing");
                world.ResetHazards();if(outward.solid)throw new BuildFailedException("Retry re-enabled old outbound route");
            }
            checks.Add("four blocks open distinct lower circuits; retry preserves the world change");
            world.Build(Movement.Rupture,0,4);if(world.hazards.Count<3||world.door==null||world.surfaces.Find(s=>s.kind==SurfaceKind.Spring)==null)throw new BuildFailedException("Escape content incomplete");
            checks.Add("telegraphed debris and white opening authored");
            ReturnSurface fragile=world.surfaces.Find(s=>s.kind==SurfaceKind.Collapse);
            motor.Reset(new Vector2(-20,0));fragile.age=0;
            for(int i=0;i<17;i++)world.Step(.1f,motor);
            if(fragile.solid)throw new BuildFailedException("Collapse did not disable after warning");
            world.ResetHazards();if(!fragile.solid||fragile.age!=-1)throw new BuildFailedException("Collapse reset failed");
            checks.Add("collapse warns, disables and resets");
            ReturnHazard impact=world.hazards[0];motor.Reset(new Vector2(impact.x,impact.y));bool contact=false;
            for(int i=0;i<150;i++)contact|=world.Step(1f/60,motor);
            if(!contact||impact.visual.gameObject.activeSelf)throw new BuildFailedException("Debris contact / expiry failed");
            world.ResetHazards();if(impact.timer!=-1)throw new BuildFailedException("Debris reset failed");
            checks.Add("debris telegraph, contact, expiry and reset");
            var skyCameraObject=new GameObject("Sky sampling validation camera");var skyCamera=skyCameraObject.AddComponent<Camera>();var sampledSky=new ReturnSky(skyCamera);
            Color initialWhite=new Color(.98f,.98f,.97f);if(QualitySettings.activeColorSpace==ColorSpace.Linear)initialWhite=initialWhite.linear;
            Color whiteSample=sampledSky.Sample(new Vector3(.3f,.7f,.2f),0),seamLeft=sampledSky.Sample(new Vector3(-1,.35f,-.00001f),1),seamRight=sampledSky.Sample(new Vector3(-1,.35f,.00001f),1),poleLeft=sampledSky.Sample(new Vector3(.00001f,1,0),1),poleRight=sampledSky.Sample(new Vector3(0,1,.00001f),1);
            if(((Vector4)(whiteSample-initialWhite)).sqrMagnitude>.000001f||((Vector4)(seamLeft-seamRight)).sqrMagnitude>.000001f||((Vector4)(poleLeft-poleRight)).sqrMagnitude>.000001f)throw new BuildFailedException("Recovered sky white phase, seam or zenith continuity failed");
            sampledSky.Dispose();UnityEngine.Object.DestroyImmediate(skyCameraObject);checks.Add("recovered panorama preserves the original white sky and remains continuous across its seam and zenith");
            var initialPipeline=QualitySettings.renderPipeline;
            var cameraObject=new GameObject("3D validation camera");var camera=cameraObject.AddComponent<Camera>();var initialClear=camera.clearFlags;var initialSky=RenderSettings.skybox;var initialAmbientMode=RenderSettings.ambientMode;var initialAmbientSky=RenderSettings.ambientSkyColor;var initialEquator=RenderSettings.ambientEquatorColor;var initialGround=RenderSettings.ambientGroundColor;var initialReflectionMode=RenderSettings.defaultReflectionMode;var initialReflection=RenderSettings.customReflectionTexture;var initialReflectionIntensity=RenderSettings.reflectionIntensity;var journey=new ReturnJourney3D(camera);journey.Begin();
            if(camera.orthographic||journey.GeometryVertexCount<40000)throw new BuildFailedException("Detailed volumetric scene or perspective camera missing");
            if(QualitySettings.renderPipeline==initialPipeline)throw new BuildFailedException("3D renderer was not activated");
            checks.Add("last two phases use perspective and authored three-dimensional geometry");
            for(int i=0;i<120&&journey.Position.y>=-.25f;i++)journey.Step(1f/60,new ReturnInput());
            if(journey.Position.y>=-.25f||journey.CanRest||Mathf.Abs(journey.LeafZ)>.001f)throw new BuildFailedException("3D fall did not enter the water before reaching the stationary leaf");
            float waterStartZ=journey.Position.z,swimDistance=0;
            for(int i=0;i<300&&!journey.CanRest;i++)
            {
                Vector2 approach=journey.DirectionTo(new Vector3(0,0,journey.LeafZ));
                journey.Step(1f/60,new ReturnInput{move=approach.x,vertical=approach.y});
                if(journey.Position.y<-.25f)swimDistance=Mathf.Max(swimDistance,Mathf.Abs(journey.Position.z-waterStartZ));
            }
            if(!journey.CanRest||swimDistance<2)throw new BuildFailedException("3D swimming did not reach and board the green leaf");
            float boardedLeafZ=journey.LeafZ;
            for(int i=0;i<60;i++)journey.Step(1f/60,new ReturnInput());
            if(!journey.CanRest||journey.Resting||Mathf.Abs(journey.LeafZ-boardedLeafZ)>.001f)throw new BuildFailedException("The leaf drifted before the player chose to rest");
            checks.Add("fall into water, swim to the leaf and board without drifting before deliberate rest");
            var traveller=GameObject.Find("The dreamer").transform;var joints=new List<Transform>();Transform hip=null;
            foreach(var joint in traveller.GetComponentsInChildren<Transform>()){if(joint.name=="pelvis")hip=joint;if(joint.name=="pelvis"||joint.name=="head"||joint.name=="hand_l"||joint.name=="hand_r"||joint.name=="foot_l"||joint.name=="foot_r")joints.Add(joint);}
            if(hip==null||joints.Count!=6)throw new BuildFailedException("Rest transition joints missing");
            var previousJoints=new Vector3[joints.Count];for(int i=0;i<joints.Count;i++)previousJoints[i]=joints[i].position;
            float initialHip=hip.position.y,lowestHip=initialHip,maxJointStep=0,maxRootTurn=0,maxStanceSlip=0,previousLeaf=journey.LeafZ;Quaternion previousRotation=traveller.rotation;
            float sampledAmount=-1,minSole=float.PositiveInfinity,maxSole=float.NegativeInfinity;int shoeSamples=0;
            System.Action sampleRest=()=>{
                float drift=journey.LeafZ-previousLeaf;maxRootTurn=Mathf.Max(maxRootTurn,Quaternion.Angle(previousRotation,traveller.rotation));previousRotation=traveller.rotation;lowestHip=Mathf.Min(lowestHip,hip.position.y);
                for(int i=0;i<joints.Count;i++){Vector3 step=joints[i].position-previousJoints[i]-Vector3.forward*drift;maxJointStep=Mathf.Max(maxJointStep,step.magnitude);if(journey.Resting&&journey.RestPoseAmount>.03f&&journey.RestPoseAmount<.38f&&joints[i].name.StartsWith("foot_"))maxStanceSlip=Mathf.Max(maxStanceSlip,new Vector2(step.x,step.z).magnitude);previousJoints[i]=joints[i].position;}previousLeaf=journey.LeafZ;
                if(journey.RestPoseAmount!=sampledAmount){Vector2 gaps=journey.ShoeClearance();if(float.IsNaN(gaps.x)||float.IsNaN(gaps.y)||gaps.x<-.015f||gaps.y<-.015f||gaps.x>.035f||gaps.y>.035f)throw new BuildFailedException("Transition shoe contact failed at amount "+journey.RestPoseAmount.ToString("F4")+": right "+gaps.x.ToString("F4")+", left "+gaps.y.ToString("F4")+", anchor "+journey.Position+", leaf "+journey.LeafZ.ToString("F4"));minSole=Mathf.Min(minSole,gaps.x,gaps.y);maxSole=Mathf.Max(maxSole,gaps.x,gaps.y);sampledAmount=journey.RestPoseAmount;shoeSamples++;}
            };
            sampleRest();
            journey.Step(1f/60,new ReturnInput{interact=true});sampleRest();float rideStart=journey.Position.z;
            for(int i=0;i<240;i++){journey.Step(1f/60,new ReturnInput());sampleRest();}
            if(!journey.Resting||journey.Position.z<rideStart+4)throw new BuildFailedException("3D leaf did not physically carry its rider");
            if(!journey.ValidateRestPose(out string restDetail))throw new BuildFailedException("Leaf resting contact failed: "+restDetail);
            checks.Add("resting body/heel mesh contact and palm-down pose: "+restDetail);
            journey.Step(1f/60,new ReturnInput{look=new Vector2(45,0)});if(journey.Yaw!=45)throw new BuildFailedException("3D orbit input failed");
            checks.Add("landing on the leaf supports resting, physical drift and an orbiting camera");
            Vector3 lockedPosition=journey.Position;float lockedLeaf=journey.LeafZ;
            journey.Step(1f/60,new ReturnInput{vertical=1});sampleRest();
            for(int i=0;i<160&&journey.RestPoseAmount>0;i++){if((journey.Position-lockedPosition-Vector3.forward*(journey.LeafZ-lockedLeaf)).sqrMagnitude>.0025f)throw new BuildFailedException("Movement escaped before the character stood up");lockedPosition=journey.Position;lockedLeaf=journey.LeafZ;journey.Step(1f/60,new ReturnInput{vertical=1});sampleRest();}
            if(journey.Resting||journey.RestPoseAmount>0||initialHip-lowestHip<.45f||maxJointStep>.09f||maxRootTurn>4.5f||maxStanceSlip>.009f)throw new BuildFailedException("Sit/recline/rise continuity failed: joint step "+maxJointStep.ToString("F4")+", root turn "+maxRootTurn.ToString("F3")+", stance slip "+maxStanceSlip.ToString("F4")+", hip lowering "+(initialHip-lowestHip).ToString("F3"));
            Vector3 standingPosition=journey.Position;for(int i=0;i<60;i++)journey.Step(1f/60,new ReturnInput{vertical=1});if(Vector3.Distance(standingPosition,journey.Position)<1)throw new BuildFailedException("Movement did not resume after standing");
            checks.Add("sit/recline/rise without snapped joints or sliding stance feet; movement resumes after standing: joint step "+maxJointStep.ToString("F4")+", root turn "+maxRootTurn.ToString("F3")+", stance slip "+maxStanceSlip.ToString("F4"));
            checks.Add("both posed shoe meshes support standing/sit/recline/rise at every changing 60 Hz pose: "+shoeSamples+" samples, clearance range "+minSole.ToString("F4")+" to "+maxSole.ToString("F4")+" m");
            journey.Hide();if(!camera.orthographic||QualitySettings.renderPipeline!=initialPipeline)throw new BuildFailedException("Restart failed to restore the side-view camera/renderer");
            if(camera.clearFlags!=initialClear||RenderSettings.skybox!=initialSky||RenderSettings.ambientMode!=initialAmbientMode||RenderSettings.ambientSkyColor!=initialAmbientSky||RenderSettings.ambientEquatorColor!=initialEquator||RenderSettings.ambientGroundColor!=initialGround||RenderSettings.defaultReflectionMode!=initialReflectionMode||RenderSettings.customReflectionTexture!=initialReflection||RenderSettings.reflectionIntensity!=initialReflectionIntensity)throw new BuildFailedException("Restart failed to restore sky, ambient lighting or environment reflections");
            checks.Add("return to the first phase restores sky, ambient lighting and environment reflections");
            checks.Add("return to the first phase restores the side-view camera");journey.Dispose();UnityEngine.Object.DestroyImmediate(cameraObject);
            var peopleRoot=new GameObject("Anatomical movement validation").transform;var volumeArt=new ReturnVolumeArt();
            var nurse=new ReturnPerson3D(volumeArt,peopleRoot,"Walking nurse",null,null,null,false,1);
            Transform left=null,right=null;foreach(var bone in nurse.root.GetComponentsInChildren<Transform>()){if(bone.name=="foot_l")left=bone;if(bone.name=="foot_r")right=bone;}
            if(left==null||right==null)throw new BuildFailedException("Anatomical feet missing");
            Vector3 velocity=new Vector3(.65f,0,-.75f).normalized*.45f,previousLeft=left.position,previousRight=right.position;int leftRun=0,rightRun=0,longestLeft=0,longestRight=0;
            for(int i=0;i<240;i++){
                float t=(i+1)/60f;nurse.Pose(velocity*t,velocity,false,false,t,0,145);
                if(i>60){leftRun=Vector3.Distance(left.position,previousLeft)<.002f?leftRun+1:0;rightRun=Vector3.Distance(right.position,previousRight)<.002f?rightRun+1:0;longestLeft=Mathf.Max(longestLeft,leftRun);longestRight=Mathf.Max(longestRight,rightRun);}
                previousLeft=left.position;previousRight=right.position;
            }
            if(longestLeft<12||longestRight<12)throw new BuildFailedException("Walking feet never remain planted for 0.2 seconds: "+longestLeft+" / "+longestRight);
            checks.Add("both anatomical feet hold world contact during a moving, diagonally facing walk");
            var mother=new ReturnPerson3D(volumeArt,peopleRoot,"Mother reaching",null,null,null,false,2);Transform hand=null,index=null,pinky=null,shoulder=null,elbow=null;
            foreach(var bone in mother.root.GetComponentsInChildren<Transform>()){if(bone.name=="hand_r")hand=bone;if(bone.name=="index_01_r")index=bone;if(bone.name=="pinky_01_r")pinky=bone;if(bone.name=="upperarm_r")shoulder=bone;if(bone.name=="lowerarm_r")elbow=bone;}
            if(hand==null||index==null||pinky==null||shoulder==null||elbow==null)throw new BuildFailedException("Anatomical reaching joints missing");
            Vector3 palm=Vector3.Cross(index.position-hand.position,pinky.position-hand.position).normalized;if(Vector3.Dot(palm,-mother.root.right*Mathf.Sign(mother.root.InverseTransformPoint(hand.position).x))<0)palm=-palm;Vector3 localPalm=hand.InverseTransformDirection(palm);
            Vector3 target=new Vector3(.36f,1.05f,-.12f),previousHand=Vector3.zero;Quaternion previousWrist=hand.rotation;float maxHandStep=0,maxWristStep=0;
            for(int i=0;i<=132;i++){float t=i/60f;mother.Pose(new Vector3(.55f,0,.1f),Vector3.zero,false,false,t,0,-155);mother.ReachHand(target,Mathf.SmoothStep(0,1,t/2.2f),true);if(i>0){maxHandStep=Mathf.Max(maxHandStep,Vector3.Distance(previousHand,hand.position));maxWristStep=Mathf.Max(maxWristStep,Quaternion.Angle(previousWrist,hand.rotation));}previousHand=hand.position;previousWrist=hand.rotation;}
            if(maxHandStep>.04f||Vector3.Distance(hand.position,target)>.04f)throw new BuildFailedException("Hospital hand snaps or misses its reachable target");
            checks.Add("the mother reaches gradually and arrives at the bedside target");
            Vector3 fingers=(index.position+pinky.position)*.5f-hand.position,towardBed=Vector3.ProjectOnPlane(target-shoulder.position,Vector3.up).normalized;
            float palmUp=Vector3.Dot(hand.TransformDirection(localPalm),Vector3.up),fingerDirection=Vector3.Dot(fingers.normalized,towardBed),elbowDrop=shoulder.position.y-elbow.position.y;
            if(palmUp<.96f||fingerDirection<.95f||maxWristStep>5||elbowDrop<.12f)throw new BuildFailedException("Bedside pose failed: palm up "+palmUp.ToString("F5")+", knuckles "+fingerDirection.ToString("F5")+", wrist step "+maxWristStep.ToString("F5")+", elbow drop "+elbowDrop.ToString("F5"));
            checks.Add("bedside palm faces up, knuckles point toward the bed, wrist turns continuously and elbow stays lowered");
            UnityEngine.Object.DestroyImmediate(peopleRoot.gameObject);volumeArt.Dispose();
            ValidateIsland(checks);
            UnityEngine.Object.DestroyImmediate(world.root.gameObject);art.Dispose();
            string root=Path.GetFullPath(Path.Combine(Application.dataPath,"../../Redesign/Evidence"));Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root,"focused-validation.txt"),"PASS\n"+string.Join("\n",checks));
            Debug.Log("[RETURN] Focused validation PASS: "+checks.Count+" checks.");
        }
        [MenuItem("Return/Build playable macOS game")]
        public static void Mac()
        {
            BuildMac(true);
        }
        public static void MacAudioProbe()
        {
            BuildMac(false);
        }
        [Serializable] sealed class ProbeMeshImportResult
        {
            public string path,beforeImporter,afterImporter;
            public string[] beforeAssets,afterAssets;
            public bool meshResolved;
        }
        static string[] ProbeAssetTypes(string path)
        {
            var types=new List<string>();
            foreach(UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if(asset!=null)types.Add(asset.GetType().Name+":"+asset.name);
            return types.ToArray();
        }
        public static void MacReimportProbeMesh()
        {
            const string path="Packages/com.unity.render-pipelines.core/Runtime/Debug/ProbeSamplingDebugMesh.fbx";
            var result=new ProbeMeshImportResult{path=path,
                beforeImporter=AssetImporter.GetAtPath(path)?.GetType().Name??"missing",beforeAssets=ProbeAssetTypes(path)};
            // Refresh only this failed import; package contents and importer settings stay intact.
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);
            result.afterImporter=AssetImporter.GetAtPath(path)?.GetType().Name??"missing";
            result.afterAssets=ProbeAssetTypes(path);
            result.meshResolved=AssetDatabase.LoadAssetAtPath<Mesh>(path)!=null;
            string output=Environment.GetEnvironmentVariable("RETURN_EVIDENCE_DIR");
            if(string.IsNullOrEmpty(output))throw new BuildFailedException("Probe import evidence directory is required");
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output,"probe-mesh-import.json"),JsonUtility.ToJson(result,true));
            if(!result.meshResolved)throw new BuildFailedException("ProbeSamplingDebugMesh did not resolve as a Mesh after its targeted reimport");
            Debug.Log("[RETURN] Probe mesh import resolved: "+result.afterImporter);
            BuildMac(false);
        }
        static void BuildMac(bool refreshValidation)
        {
            const string qualityPath="ProjectSettings/QualitySettings.asset";byte[] qualitySource=File.ReadAllBytes(qualityPath);
            if(refreshValidation){ReturnBlendNormalRepair.EnsureImported();Scene();ReturnHospitalCollisionAssets.Prepare();Validate();ReturnNormalRestSnapshotBuild.Capture();}
            else if(!File.Exists(ScenePath))throw new BuildFailedException("Existing RETURN scene missing for audio probe build");
            string root=Path.GetFullPath(Path.Combine(Application.dataPath,"../../Redesign"));string output=Environment.GetEnvironmentVariable("RETURN_BUILD_OUTPUT");if(string.IsNullOrEmpty(output))output=Path.Combine(root,"Build/Return.app");output=Path.GetFullPath(output);Directory.CreateDirectory(Path.GetDirectoryName(output));
            int width=PlayerSettings.defaultScreenWidth,height=PlayerSettings.defaultScreenHeight;
            FullScreenMode mode=PlayerSettings.fullScreenMode;bool background=PlayerSettings.runInBackground;
            string product=PlayerSettings.productName;
            int originalQuality=QualitySettings.GetQualityLevel(),retentionQuality=-1;RenderPipelineAsset retentionOriginal=null;bool retentionInstalled=false;
            try
            {
                // URP strips renderer resources using enabled platform quality assets, rather
                // than every pipeline referenced by a scene. Retain RETURN's 3D renderer on
                // an alternate tier during export; the player's default side-view tier stays intact.
                var defaultMatch=System.Text.RegularExpressions.Regex.Match(System.Text.Encoding.UTF8.GetString(qualitySource),@"(?m)^\s+Standalone: (\d+)\s*$");
                if(!defaultMatch.Success)throw new BuildFailedException("Standalone default quality tier missing");
                int playerDefault=int.Parse(defaultMatch.Groups[1].Value);var sidePipeline=QualitySettings.GetRenderPipelineAssetAt(playerDefault);
                foreach(int tier in QualitySettings.GetActiveQualityLevelsForPlatform("Standalone"))if(tier!=playerDefault&&tier!=originalQuality){retentionQuality=tier;break;}
                if(retentionQuality<0)throw new BuildFailedException("No alternate enabled quality tier for 3D resource retention");
                var volume=Resources.Load<RenderPipelineAsset>("ReturnVolumePipeline");if(volume==null)throw new BuildFailedException("RETURN volume pipeline missing");
                retentionOriginal=QualitySettings.GetRenderPipelineAssetAt(retentionQuality);retentionInstalled=true;
                QualitySettings.SetQualityLevel(retentionQuality,false);QualitySettings.renderPipeline=volume;QualitySettings.SetQualityLevel(originalQuality,false);
                var included=new List<RenderPipelineAsset>();if(!UnityEditor.Rendering.BuildTargetExtensions.TryGetRenderPipelineAssets(BuildTarget.StandaloneOSX,included)||!included.Contains(volume)||(sidePipeline!=null&&!included.Contains(sidePipeline))||QualitySettings.GetRenderPipelineAssetAt(playerDefault)!=sidePipeline)throw new BuildFailedException("Build renderer retention or default side-view tier failed");
                Debug.Log("[RETURN] Build retains side-view and 3D renderer resources; player default tier preserved.");
                PlayerSettings.productName="Return";
                PlayerSettings.defaultScreenWidth=1280;PlayerSettings.defaultScreenHeight=720;PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.runInBackground=true;
                BuildReport report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName=output,target=BuildTarget.StandaloneOSX,options=BuildOptions.StrictMode});
                if(report.summary.result!=BuildResult.Succeeded)throw new BuildFailedException("Return build failed: "+report.summary.result);
                File.WriteAllText(Path.Combine(root,"Evidence/build-result.txt"),"PASS\nmacOS standalone\n"+output+"\n"+report.summary.totalSize+" bytes\n"+report.summary.totalTime);
                Debug.Log("[RETURN] BUILD PASS "+output);
            }
            finally{if(retentionInstalled){QualitySettings.SetQualityLevel(retentionQuality,false);QualitySettings.renderPipeline=retentionOriginal;QualitySettings.SetQualityLevel(originalQuality,false);}PlayerSettings.productName=product;PlayerSettings.defaultScreenWidth=width;PlayerSettings.defaultScreenHeight=height;PlayerSettings.fullScreenMode=mode;PlayerSettings.runInBackground=background;AssetDatabase.SaveAssets();File.WriteAllBytes(qualityPath,qualitySource);}
        }
    }
}
