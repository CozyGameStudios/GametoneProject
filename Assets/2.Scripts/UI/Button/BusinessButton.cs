using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BusinessButton : MonoBehaviour
{
    public void TutorialButtonClick()
    {
        if (BusinessGameManager.Instance.currentBusinessStage == 1)
        {
            TutorialManager.Instance.BusinessButtonTouch();
            Debug.Log("Click");
        }
    }
}
