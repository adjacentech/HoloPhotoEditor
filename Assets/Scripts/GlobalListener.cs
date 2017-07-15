using HoloToolkit.Unity.InputModule;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class GlobalListener : MonoBehaviour, IInputClickHandler, ISourceStateHandler
{
    public void OnInputClicked(InputClickedEventData eventData)
    {
        if (CapturePhotoManager.Instance.GetCurrentStatus() == CurrentStatus.WaitingTakingPhoto)
        {
            CapturePhotoManager.Instance.TakePhoto();
        }
    }

    public void OnSourceDetected(SourceStateEventData eventData)
    {
        if (CapturePhotoManager.Instance.GetCurrentStatus() == CurrentStatus.Ready)
        {
            CapturePhotoManager.Instance.SetCurrentStatus(CurrentStatus.WaitingTakingPhoto);
            ModelManager.Instance.SetTipText("air tap to take a photo");
            ModelManager.Instance.ResetCropBoxTransform();
            ModelManager.Instance.SetPhotoImageActive(false);
            ModelManager.Instance.SetCropBoxActive(false);
            ToolManager.Instance.HideMenu();
        }
    }

    public void OnSourceLost(SourceStateEventData eventData)
    {
    }

    // Use this for initialization
    void Start () {
        InputManager.Instance.AddGlobalListener(gameObject);
    }
	
	// Update is called once per frame
	void Update () {
		
	}
}
