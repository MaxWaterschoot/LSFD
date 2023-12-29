using LSFD.MainClasses;
using Newtonsoft.Json;
using Rage;

[assembly: Rage.Attributes.Plugin("LSFD", Description = "This is a LSFD plugin. It's also my first plugin.", Author = "DeEchteBelg")]


namespace LSFD
{

    
    public class EntryPoint
    {

        static void Main() 
        {
            Game.LogTrivialDebug("LSFD is loading.");


            Rage.GameFiber.Yield();

            Game.LogTrivialDebug("LSFD has been loaded in.");
        }
        private static void OnUnload(bool isTerminating)
        {
            Game.LogTrivialDebug("LSFD is unloading");
        }

    }
}
