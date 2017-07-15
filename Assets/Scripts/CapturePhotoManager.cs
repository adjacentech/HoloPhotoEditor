using HoloToolkit.Unity;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.VR.WSA.WebCam;

public class CapturePhotoManager : Singleton<CapturePhotoManager>
{
    Resolution cameraResolution;
    PhotoCapture photoCaptureObject = null;
    CameraParameters cameraParameters;

    private CurrentStatus currentStatus = CurrentStatus.Ready;

    public GameObject cropBoxObject;

    public AudioClip captureAudioClip;
    public AudioClip failedAudioClip;
    private AudioSource audioSource;

    private List<byte> imageBufferList = new List<byte>();

    // Use this for initialization
    void Start () {
        audioSource = gameObject.GetComponent<AudioSource>();
        cameraResolution = PhotoCapture.SupportedResolutions.OrderByDescending((res) => res.width * res.height).First();
    }

    /// <summary>
    /// 开始拍照流程
    /// </summary>
    public void TakePhoto()
    {        
        PhotoCapture.CreateAsync(false, OnPhotoCaptureCreated);
    }

    /// <summary>
    /// 获取当前状态
    /// </summary>
    /// <returns>当前状态值</returns>
    public CurrentStatus GetCurrentStatus()
    {
        return currentStatus;
    }

    /// <summary>
    /// 设置当前状态
    /// </summary>
    /// <param name="status">状态值</param>
    public void SetCurrentStatus(CurrentStatus status)
    {
        currentStatus = status;
    }

    /// <summary>
    /// 设置Camera参数，开始拍照
    /// </summary>
    /// <param name="captureObject"></param>
    void OnPhotoCaptureCreated(PhotoCapture captureObject)
    {        
        ModelManager.Instance.SetTipText("taking a photo...");
        currentStatus = CurrentStatus.TakingPhoto;
        photoCaptureObject = captureObject;
        cameraParameters = new CameraParameters();
        cameraParameters.hologramOpacity = 0.0f;
        cameraParameters.cameraResolutionWidth = cameraResolution.width;
        cameraParameters.cameraResolutionHeight = cameraResolution.height;
        cameraParameters.pixelFormat = CapturePixelFormat.BGRA32;

        captureObject.StartPhotoModeAsync(cameraParameters, OnPhotoModeStarted);
    }

    /// <summary>
    /// 开始拍照
    /// </summary>
    /// <param name="result">拍照结果</param>
    private void OnPhotoModeStarted(PhotoCapture.PhotoCaptureResult result)
    {
        if (result.success)
        {
            photoCaptureObject.TakePhotoAsync(OnCapturedPhotoToMemory);
        }
        else
        {
            ModelManager.Instance.SetTipText("air tap to take a photo");
            currentStatus = CurrentStatus.Ready;
            ModelManager.Instance.SetPhotoImageActive(false);
        }
    }

    /// <summary>
    /// 照片拍摄完成，获取拍摄的照片，调用Custom Vision API,对图片进行分析
    /// </summary>
    /// <param name="result">拍照的结果</param>
    /// <param name="photoCaptureFrame">拍摄的图片</param>
    private void OnCapturedPhotoToMemory(PhotoCapture.PhotoCaptureResult result, PhotoCaptureFrame photoCaptureFrame)
    {
        if (result.success)
        {
            audioSource.Stop();
            audioSource.clip = captureAudioClip;
            audioSource.Play();

            ModelManager.Instance.SetPhotoImageActive(true);
            ModelManager.Instance.SetTipText("editing...");

            ToolManager.Instance.ShowMenu();
            currentStatus = CurrentStatus.EdittingPhoto;

            photoCaptureFrame.CopyRawImageDataIntoBuffer(imageBufferList);
            imageBufferList = FlipVertical(imageBufferList, cameraParameters.cameraResolutionWidth, cameraParameters.cameraResolutionHeight, 4);

            Texture2D targetTexture = CreateTexture(imageBufferList, cameraParameters.cameraResolutionWidth, cameraParameters.cameraResolutionHeight);
            Sprite sprite = Sprite.Create(targetTexture, new Rect(0, 0, targetTexture.width, targetTexture.height), new Vector2(0.5f, 0.5f));

            ModelManager.Instance.SetPhotoImage(sprite);
        }
        else
        {
            audioSource.Stop();
            audioSource.clip = failedAudioClip;
            audioSource.Play();

            currentStatus = CurrentStatus.Ready;
            ModelManager.Instance.SetTipText("air tap to take a photo");
        }
        photoCaptureObject.StopPhotoModeAsync(OnStoppedPhotoMode);
    }

    /// <summary>
    /// 拍照结束，释放资源
    /// </summary>
    /// <param name="result">result</param>
    private void OnStoppedPhotoMode(PhotoCapture.PhotoCaptureResult result)
    {
        photoCaptureObject.Dispose();
        photoCaptureObject = null;
    }

    public void ShowEditedImage()
    {
        bool cropBoxActiveFlag = ModelManager.Instance.IsCropBoxActive();
        RectTransform rectTransform = cropBoxObject.GetComponent<CropBoxManager>().GetCropBoxRectTransform();
        Vector3 cropBoxLocalPosition = new Vector3(rectTransform.localPosition.x, rectTransform.localPosition.y, rectTransform.localPosition.z);
        Vector2 cropBoxSize = new Vector2(rectTransform.rect.width, rectTransform.rect.height);
        Vector2 parentSize = cropBoxObject.GetComponent<CropBoxManager>().GetParentSize();

        if (cropBoxActiveFlag)
        {
            int leftSide = (int)((cropBoxLocalPosition.x - cropBoxSize.x / 2 + parentSize.x / 2) / parentSize.x * cameraParameters.cameraResolutionWidth);
            int rightSide = (int)((parentSize.x / 2 + cropBoxLocalPosition.x + cropBoxSize.x / 2) / parentSize.x * cameraParameters.cameraResolutionWidth);
            int bottomSide = (int)((cropBoxLocalPosition.y - cropBoxSize.y / 2 + parentSize.y / 2) / parentSize.y * cameraParameters.cameraResolutionHeight);
            int topSide = (int)((parentSize.y / 2 + cropBoxLocalPosition.y + cropBoxSize.y / 2) / parentSize.y * cameraParameters.cameraResolutionHeight);
            //用于显示
            byte[] dst = new byte[imageBufferList.Count];
            //用于上传
            byte[] dstpost = new byte[(rightSide - leftSide + 1) * (topSide - bottomSide + 1) * 4];
            int count = 0;
            for (int y = 0; y < cameraParameters.cameraResolutionHeight; ++y)
            {
                for (int x = 0; x < cameraParameters.cameraResolutionWidth; ++x)
                {
                    int px = (y * cameraParameters.cameraResolutionWidth + x) * 4;
                    if (x >= leftSide && x <= rightSide && y >= bottomSide && y <= topSide)
                    {
                        int index = count * 4;
                        for (int i = 0; i < 4; ++i)
                        {
                            dst[px + i] = imageBufferList[px + i];
                            dstpost[index + i] = imageBufferList[px + i];
                        }
                        count++;
                    }
                    else
                    {
                    }
                }
            }

            Texture2D targetTexture = new Texture2D(cameraParameters.cameraResolutionWidth, cameraParameters.cameraResolutionHeight, TextureFormat.BGRA32, false);
            targetTexture.LoadRawTextureData(dst);
            targetTexture.Apply();
            Sprite sprite = Sprite.Create(targetTexture, new Rect(0, 0, targetTexture.width, targetTexture.height), new Vector2(0.5f, 0.5f));
            ModelManager.Instance.SetPhotoImage(sprite);
            ModelManager.Instance.SetCropBoxActive(false);

            Texture2D postTexture = new Texture2D(rightSide - leftSide + 1, topSide - bottomSide + 1, TextureFormat.BGRA32, false);
            postTexture.LoadRawTextureData(dstpost);
            postTexture.Apply();
        }
        ModelManager.Instance.SetTipText("air tap to take a photo");
        currentStatus = CurrentStatus.Ready;
    }

    /// <summary>
    /// 创建Texture2D
    /// </summary>
    /// <param name="rawData">图像数据</param>
    /// <param name="width">宽度</param>
    /// <param name="height">高度</param>
    /// <returns></returns>
    private Texture2D CreateTexture(List<byte> rawData, int width, int height)
    {
        Texture2D tex = new Texture2D(width, height, TextureFormat.BGRA32, false);
        tex.LoadRawTextureData(rawData.ToArray());
        tex.Apply();
        return tex;
    }

    /// <summary>
    /// 上下反转
    /// </summary>
    /// <param name="src">图像数据</param>
    /// <param name="width">宽度</param>
    /// <param name="height">高度</param>
    /// <param name="stride">每个像素的宽度</param>
    /// <returns></returns>
    private List<byte> FlipVertical(List<byte> src, int width, int height, int stride)
    {
        byte[] dst = new byte[src.Count];
        for (int y = 0; y < height; ++y)
        {
            for (int x = 0; x < width; ++x)
            {
                int invY = (height - 1) - y;
                int pxel = (y * width + x) * stride;
                int invPxel = (invY * width + x) * stride;
                for (int i = 0; i < stride; ++i)
                {
                    dst[invPxel + i] = src[pxel + i];
                }
            }
        }
        return new List<byte>(dst);
    }

    // Update is called once per frame
    void Update () {
		
	}
}
