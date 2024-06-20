using UnityEngine;
using System.Collections;
using System.Linq;
using TMPro;

public class Log : MonoBehaviour
{
    uint logSize = 100; 
    Queue logQueue = new Queue();
    public TMP_Text console;
    
    void OnEnable() {
        Application.logMessageReceived += HandleLog;
    }

    void OnDisable() {
        Application.logMessageReceived -= HandleLog;
    }

    void HandleLog(string logString, string stackTrace, LogType type) {
        logQueue.Enqueue("[" + type + "] : " + logString);
        if (type == LogType.Exception)
            logQueue.Enqueue(stackTrace);
        while (logQueue.Count > logSize)
            logQueue.Dequeue();
        console.text = "\n" + string.Join("\n", logQueue.ToArray().Reverse());
    }
    
}