using UnityEngine;
namespace RoyalFortress.GateOfDead
{
    public sealed class GateV2LayerPlayback : MonoBehaviour
    {
        public GateOfDeadPresentation presentation;
        public SpriteRenderer[] gateRunes, altarRunes;
        public float bossBeamWidth = .62f;
        private void LateUpdate() { Apply(); }
        public void Apply()
        {
            if (!presentation) return;
            SetAlpha(gateRunes, presentation.RuneAlpha(false));
            SetAlpha(altarRunes, presentation.RuneAlpha(true));
            var beam=presentation.bossBeam;
            if(beam && beam.enabled)
            {
                Vector3 start=presentation.bossSocket.position, end=presentation.ClashPosition, delta=end-start;
                beam.drawMode=SpriteDrawMode.Simple;
                beam.transform.position=(start+end)*.5f;
                beam.transform.rotation=Quaternion.Euler(0,0,Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg+90);
                Vector2 size=beam.sprite.bounds.size;
                beam.transform.localScale=new Vector3(bossBeamWidth/size.x,delta.magnitude/size.y,1);
            }
        }
        static void SetAlpha(SpriteRenderer[] sprites,float alpha)
        { if(sprites==null)return; foreach(var sprite in sprites)if(sprite)sprite.color=new Color(1,1,1,alpha*.75f); }
    }
}
