using System.Collections.Generic;
using UnityEngine;

namespace Return.Redesign
{
    public enum SurfaceKind { Stone, Fading, Collapse, Ferry, Spring }
    public sealed class ReturnSurface
    {
        public float x, width, y, originX, originY, age=-1, span, phase;
        public SurfaceKind kind;
        public Transform visual;
        public bool solid=true,returnPath;
        public float Left=>x-width*.5f;
        public float Right=>x+width*.5f;
        public ReturnSurface(float center,float top,float length,SurfaceKind type=SurfaceKind.Stone) {x=originX=center;y=originY=top;width=length;kind=type;}
        public void Reset() {x=originX;y=originY;age=-1;solid=true;}
    }
    // Feet-position motor with swept landing collision, coyote time and buffered variable jumps.
    public sealed class ReturnMotor
    {
        public const float Radius=.24f, Height=1.32f, Speed=6.8f, JumpSpeed=12.5f, Gravity=29f;
        public const float GroundBrake=90f, AirBrake=64f;
        public Vector2 position,velocity;
        public ReturnSurface support;
        public bool Grounded=>support!=null;
        public bool swimming;
        public float facing=1;
        public bool BurstReady {get;private set;}=true;
        public bool Bursting=>burst>0;
        public bool BurstStarted {get;private set;}
        public float LandingImpact {get;private set;}
        float coyote,buffer,burst,burstDirection,burstBuffer,bufferedBurstDirection;
        internal ReturnMotor Snapshot(){return (ReturnMotor)MemberwiseClone();}
        public void Reset(Vector2 p) {position=p;velocity=Vector2.zero;support=null;coyote=buffer=burst=burstBuffer=0;BurstReady=true;BurstStarted=false;LandingImpact=0;}
        public void Step(float dt,float move,float vertical,bool jumpPressed,bool jumpHeld,List<ReturnSurface> ground,bool burstPressed=false)
        {
            BurstStarted=false;
            LandingImpact=Mathf.MoveTowards(LandingImpact,0,dt*4);
            if(Grounded)BurstReady=true;
            if(jumpPressed) buffer=.14f;else buffer=Mathf.Max(0,buffer-dt);
            if(Grounded)coyote=.12f;else coyote=Mathf.Max(0,coyote-dt);
            if(Mathf.Abs(move)>.05f)facing=Mathf.Sign(move);
            // A press just before landing survives until ground contact restores the burst.
            if(swimming)burstBuffer=0;
            else if(burstPressed){burstBuffer=.12f;bufferedBurstDirection=facing;}
            else burstBuffer=Mathf.Max(0,burstBuffer-dt);
            if(burstBuffer>0&&BurstReady&&!swimming){burstBuffer=0;burst=.17f;burstDirection=bufferedBurstDirection;BurstReady=false;BurstStarted=true;support=null;coyote=0;velocity=new Vector2(burstDirection*16,0);}
            if(burst>0){burst=Mathf.Max(0,burst-dt);velocity=new Vector2(burstDirection*(burst>0?16:Speed),0);}
            else
            {
            float acceleration=Grounded?60:38;
            // Releasing or reversing should give control over a narrow landing.
            if(!swimming&&(Mathf.Abs(move)<.05f||velocity.x*move<0))acceleration=Grounded?GroundBrake:AirBrake;
            velocity.x=Mathf.MoveTowards(velocity.x,move*(swimming?3.5f:Speed),acceleration*dt);
            if(swimming)
            {
                support=null;float target=vertical*3.4f+(jumpHeld?2.3f:0);
                velocity.y=Mathf.MoveTowards(velocity.y,target+(position.y<-.9f?.55f:0),7*dt);
            }
            else
            {
                if(buffer>0&&coyote>0){velocity.y=JumpSpeed;buffer=0;coyote=0;support=null;}
                velocity.y-=Gravity*dt;
                if(!jumpHeld&&velocity.y>5) velocity.y-=Gravity*.8f*dt;
            }
            }
            Vector2 before=position,next=position+velocity*dt;
            ReturnSurface landed=null;float highest=float.NegativeInfinity;
            foreach(ReturnSurface s in ground)
            {
                if(!s.solid)continue;
                if(next.x+Radius>s.Left && next.x-Radius<s.Right)
                {
                    if(velocity.y<=0&&before.y>=s.y-.035f&&next.y<=s.y&&s.y>highest){landed=s;highest=s.y;}
                }
                if(before.y<s.y-.05f&&before.y+Height>s.y-.7f && next.y<s.y)
                {
                    if(before.x+Radius<=s.Left && next.x+Radius>s.Left){next.x=s.Left-Radius;velocity.x=0;}
                    else if(before.x-Radius>=s.Right && next.x-Radius<s.Right){next.x=s.Right+Radius;velocity.x=0;}
                }
            }
            if(landed!=null)
            {
                if(support==null)LandingImpact=Mathf.Clamp01(-velocity.y/18);
                next.y=landed.y;velocity.y=0;support=landed;BurstReady=true;
                if(landed.kind==SurfaceKind.Spring){velocity.y=18.5f;support=null;}
                if(landed.age<0)landed.age=0;
            }
            else support=null;
            position=next;
        }
    }
}
