using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EventManager : UnitySingleton<EventManager>
{
    private Dictionary<string, Action<object>> _eventDict = new Dictionary<string, Action<object>>();

    /*
     * 增加监听
     */
    public void AddListener(string eventName, Action<object> callback)
    {
        if (!_eventDict.ContainsKey(eventName)) _eventDict[eventName] = null;
        _eventDict[eventName] += callback;
    }

    /*
     * 触发事件
     */
    public void Emit(string eventName, object arg = null)
    {
        if (_eventDict.ContainsKey(eventName)) _eventDict[eventName]?.Invoke(arg);
    }
}