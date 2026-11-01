using UnityEngine;

namespace Return.Redesign {
 public static class ReturnConstructionSurface {
  public static Material Material(ReturnVolumeArt art,string name,Color tint) {
   var source=Resources.Load<Material>("ReturnConstruction/"+name);
   if(source==null)throw new System.InvalidOperationException("Construction material missing: "+name);
   var material=art.Copy(source);material.color=tint;return material;
  }
 }
}
