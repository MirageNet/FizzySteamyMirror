using UnityEngine;

namespace Mirage.Sockets.FizzySteam
{
    public static class NetworkPrinter
    {
        public static void Log(string message)
        {
            Debug.Log($"<color=#0099CC>{message}</color>");
        }
        
        public static void Warn(string message)
        {
            Debug.Log($"<color=#ffa500>{message}</color>");
        }
    }
}