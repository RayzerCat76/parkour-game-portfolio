using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Return.Redesign
{
    // Runs only in the explicitly requested native collision/timing review.
    static class ReturnRuptureHazardReview
    {
        [Serializable] sealed class Trial {public float meteorX,stepSeconds,warningSeconds,hitX;public bool reacted,hit,warningSeen;}
        [Serializable] sealed class Result {public bool passed,cracksKeepDepth,resetClearsDust;public int meteors,cracks;public string error;public Trial[] trials;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Run()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"--return-review-rupture-hazards")<0)return;
            var result=new Result();var trials=new List<Trial>();
            try
            {
                var game=UnityEngine.Object.FindFirstObjectByType<ReturnGame>();if(game==null)throw new Exception("Actual ReturnGame missing");
                game.enabled=false;var world=game.world;world.Build(Movement.Rupture,0,4);
                foreach(var hazard in world.hazards)
                {
                    if(!hazard.debris)continue;result.meteors++;
                    foreach(float dt in new[]{1f/60,1f/120})foreach(bool reacted in new[]{false,true})
                    {
                        world.ResetHazards();var motor=new ReturnMotor();motor.Reset(new Vector2(hazard.x-16,hazard.y));
                        var trial=new Trial{meteorX=hazard.x,stepSeconds=dt,reacted=reacted};float warningStart=-1,elapsed=0;
                        while(elapsed<7&&motor.position.x<hazard.x+3)
                        {
                            // Fixed level paths isolate meteor timing from gaps, jump physics and the automated route.
                            bool wait=reacted&&hazard.timer>=0&&hazard.timer<2.45f&&hazard.x-motor.position.x<4;
                            motor.velocity=new Vector2(wait?0:ReturnMotor.Speed,0);motor.position+=motor.velocity*dt;elapsed+=dt;
                            bool hit=world.Step(dt,motor);
                            if(hazard.warning.gameObject.activeSelf&&warningStart<0){warningStart=elapsed;trial.warningSeen=true;}
                            if(hit){trial.hit=true;trial.hitX=motor.position.x;trial.warningSeconds=elapsed-warningStart;break;}
                        }
                        trials.Add(trial);
                        if(!trial.warningSeen||trial.hit==reacted||(!reacted&&trial.warningSeconds<.7f))throw new Exception("Meteor timing did not require a readable reaction at x="+hazard.x+", reacted="+reacted+", dt="+dt);
                    }
                }
                if(result.meteors!=3)throw new Exception("Expected the three actual Rupture meteors");
                world.ResetHazards();var shelf=world.surfaces.Find(s=>s.kind==SurfaceKind.Collapse);if(shelf==null)throw new Exception("Collapse platform missing");
                shelf.age=.9f;var standing=new ReturnMotor();standing.Reset(new Vector2(shelf.x,shelf.y));standing.support=shelf;world.Step(0,standing);
                result.cracksKeepDepth=true;
                foreach(var transform in shelf.visual.GetComponentsInChildren<Transform>(true))if(transform.name=="spreading ground fissure"&&transform.gameObject.activeSelf){result.cracks++;result.cracksKeepDepth&=Mathf.Abs(transform.localScale.y-1)<.0001f;}
                if(result.cracks!=4||!result.cracksKeepDepth)throw new Exception("Collapse cue stretches its fissures or is missing");
                world.ResetHazards();result.resetClearsDust=world.hazards.TrueForAll(h=>h.impact==null||!h.impact.gameObject.activeSelf);
                if(!result.resetClearsDust)throw new Exception("Reset retained meteor dust");
                result.passed=true;
            }
            catch(Exception error){result.error=error.Message;}
            result.trials=trials.ToArray();string directory=Environment.GetEnvironmentVariable("RETURN_EVIDENCE_DIR");
            if(string.IsNullOrEmpty(directory))directory=Path.Combine(Application.persistentDataPath,"ReturnEvidence");Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory,"rupture-hazard-review.json"),JsonUtility.ToJson(result,true));
            Debug.Log("[RETURN rupture-hazards] "+JsonUtility.ToJson(result));Application.Quit(result.passed?0:1);
        }
    }
}
