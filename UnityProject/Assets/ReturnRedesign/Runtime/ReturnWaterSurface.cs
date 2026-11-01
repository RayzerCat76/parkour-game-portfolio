using UnityEngine;
namespace Return.Redesign {
 public static class ReturnWaterSurface {
  public const float BaseHeight=-.11f;
  // Wavenumber X/Z, angular speed and height amplitude; supplied to every GPU pass.
  static readonly Vector4[] waves={new Vector4(.23f,.095f,.62f,.026f),new Vector4(-.13f,.32f,.51f,.021f),new Vector4(.41f,-.17f,.84f,.014f),new Vector4(-.36f,-.28f,.73f,.009f)};
  public static void Configure(Material material){for(int i=0;i<waves.Length;i++)material.SetVector("_ReturnWave"+i,waves[i]);}
  public static float Height(float x,float z,float time) {
   float bend=.35f*Mathf.Sin(x*.037f+z*.051f-time*.07f),height=BaseHeight;
   for(int i=0;i<waves.Length;i++){Vector4 wave=waves[i];float phase=x*wave.x+z*wave.y-time*wave.z+.4f+i*1.7f+((i&1)==0?bend:-bend);height+=wave.w*Mathf.Sin(phase);}
   return height;
  }
 }
}
