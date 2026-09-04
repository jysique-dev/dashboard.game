using LoopEngine.LogEngine;
using UnityObject = UnityEngine.Object;

public static class LoopLog
{
    public static void Error(string channel, string message, UnityObject context = null)
    {
        Log.Error(channel,message, context);
    }
    public static void Warning(string channel, string message, UnityObject context = null)
    {
        Log.Warning(channel, message, context);
    }
    public static void Info(string channel, string message, UnityObject context = null)
    {
        Log.Info(channel, message, context);
    }
    public static void Trace(string channel, string message, UnityObject context = null)
    {
        Log.Trace(channel, message, context);
    }
    public static void Critical(string channel, string message, UnityObject context = null)
    {
        Log.Critical(channel, message, context);
    }
    public static void Exception(System.Exception exception, UnityObject context = null)
    {
        Log.Exception(exception, context);
    }
}
