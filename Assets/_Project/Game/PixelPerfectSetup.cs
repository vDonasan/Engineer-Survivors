using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ES.Game
{
    /// <summary>
    /// Câmera pixel perfect do URP para a pixel art 32×32 (PPU 32): referência
    /// 640×360 (escala inteira ×3 em 1080p), pixel snapping, sem HDR nem MSAA.
    /// </summary>
    public static class PixelPerfectSetup
    {
        public const int AssetsPPU = 32;
        public const int ReferenceWidth = 640;
        public const int ReferenceHeight = 360;

        public static PixelPerfectCamera Apply(Camera cam)
        {
            cam.orthographic = true;
            cam.allowHDR = false;
            cam.allowMSAA = false;
            cam.allowDynamicResolution = false;
            var ppc = cam.GetComponent<PixelPerfectCamera>();
            if (ppc == null) ppc = cam.gameObject.AddComponent<PixelPerfectCamera>();
            ppc.assetsPPU = AssetsPPU;
            ppc.refResolutionX = ReferenceWidth;
            ppc.refResolutionY = ReferenceHeight;
            ppc.gridSnapping = PixelPerfectCamera.GridSnapping.PixelSnapping;
            ppc.cropFrame = PixelPerfectCamera.CropFrame.None;
            return ppc;
        }
    }
}
