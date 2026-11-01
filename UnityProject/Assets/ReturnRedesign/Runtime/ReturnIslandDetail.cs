using System.Collections.Generic;
using UnityEngine;
namespace Return.Redesign {
 public static class ReturnIslandDetail {
  public static void Create(ReturnVolumeArt art,Transform parent) {
   var rng=new System.Random(714);var green=art.Material(new Color(.27f,.42f,.12f),.19f);green.SetFloat("_RecoveryChannel",3);
   var yellow=art.Material(new Color(.99f,.72f,.05f),.22f);yellow.SetFloat("_RecoveryChannel",1);
   var red=art.Material(new Color(.82f,.065f,.025f),.24f);red.SetFloat("_RecoveryChannel",2);
   yellow.SetFloat("_ThinSurface",.65f);red.SetFloat("_ThinSurface",.65f);
   // The exported cover follows the support surface and preserves the worn fruit approach.
   var grass=art.Model("IslandGrass",parent,new Vector3(0,0,26));grass.localRotation=Quaternion.Euler(0,180,0)*grass.localRotation;
   var stones=new[]{new Vector2(-3.05f,-.75f),new Vector2(-3.25f,-.4f),new Vector2(-2.85f,-.55f),new Vector2(-3.4f,.3f),new Vector2(-3.12f,.5f),new Vector2(-2.87f,1.85f),new Vector2(-2.64f,2.07f),new Vector2(2.72f,1.6f),new Vector2(2.85f,1.3f),new Vector2(3.18f,-.18f),new Vector2(2.91f,-.35f),new Vector2(-1.4f,-2.92f)};
   for(int i=0;i<stones.Length;i++) {
     Vector3 p=new Vector3(stones[i].x,ReturnVolumeArt.IslandHeight(stones[i].x,stones[i].y),stones[i].y+26);
     var stone=art.Model("ShoreRock",parent,p-Vector3.up*.045f);float scale=.27f+(float)rng.NextDouble()*.3f;
     stone.localScale=Vector3.Scale(stone.localScale,new Vector3(scale,scale*(.83f+(float)rng.NextDouble()*.2f),scale));
     stone.localRotation=Quaternion.Euler(0,(float)rng.NextDouble()*360,0)*stone.localRotation;stone.name="Scanned shore stone";
     foreach(var renderer in stone.GetComponentsInChildren<Renderer>()){var material=art.Copy(renderer.sharedMaterial);material.SetFloat("_ShoreSoil",1);renderer.sharedMaterial=material;}
   }
   var stands=new[]{new Vector2(-2.3f,-.65f),new Vector2(1.95f,1.25f),new Vector2(-.3f,2.55f)};
   for(int i=0;i<15;i++) {
     float angle=i*2.399f,r=.1f+(float)rng.NextDouble()*.33f;Vector2 stand=stands[i/5]+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*r;
     Vector3 p=new Vector3(stand.x,ReturnVolumeArt.IslandHeight(stand.x,stand.y),stand.y+26);float height=.22f+(float)rng.NextDouble()*.12f;
     art.Tube("Flower stem",parent,p,p+Vector3.up*height,.004f,green);var petals=new List<Vector3>();var triangles=new List<int>();
     const int rows=8,columns=6;
     for(int j=0;j<6;j++){float theta=j*Mathf.PI/3+angle;Vector3 centre=p+Vector3.up*height,along=new Vector3(Mathf.Cos(theta),0,Mathf.Sin(theta)),across=new Vector3(-Mathf.Sin(theta),0,Mathf.Cos(theta));int start=petals.Count;
      for(int row=0;row<=rows;row++)for(int col=0;col<=columns;col++){float u=row/(float)rows,t=col/(float)columns*2-1,s=Mathf.Sin(u*Mathf.PI);petals.Add(centre+along*(.018f+.075f*u)+across*(.037f*Mathf.Pow(Mathf.Max(0,s),.65f)*t)+Vector3.up*(.028f*s-.009f*u+.012f*t*t*s));if(row<rows&&col<columns){int k=start+row*(columns+1)+col;triangles.AddRange(new[]{k,k+1,k+columns+1,k+1,k+columns+2,k+columns+1});}}
     }
     art.Mesh(i%2==0?"Yellow wildflower":"Red wildflower",parent,petals.ToArray(),triangles.ToArray(),i%2==0?yellow:red);
   }
  }
 }
}
