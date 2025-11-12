using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using EyE.Threading;


public class TaskContextDisplay : MonoBehaviour
{
    public Text stageText;
    public Transform spinIndicator;
    public Button cancel;
    TaskHandler context;

    public void SetContext(TaskHandler context)
    {
        this.context = context;
        if (context.IsComplete || context.IsCancellationRequested)
        {
            gameObject.SetActive(false);
            return;
        }
        stageText.text = context.StageMessage;
        gameObject.SetActive(true);
    }

    // Start is called before the first frame update
    void Start()
    {
        cancel.onClick.AddListener(DoCancel);
    }
    void DoCancel()
    {
        Debug.Log("Canceling");
        context.DoCancel();
    }
    // Update is called once per frame
    void Update()
    {
        if (context.IsCancellationRequested)
            stageText.text = "Canceling Processes";
        else
        {
            stageText.text = context.StageMessage;
            spinIndicator.localRotation = Quaternion.Euler(new Vector3(0, 0, context.ProgressValue));
        }
        
        if (context.IsComplete || !context.IsRunning)
            gameObject.SetActive(false);

    }
}
