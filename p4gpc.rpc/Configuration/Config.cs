using p4gpc.rpc.Configuration.Implementation;
using System.ComponentModel;

namespace p4gpc.rpc.Configuration
{
    public class Config : Configurable<Config>
    {
        /*
            User Properties:
                - Please put all of your configurable properties here.
                - Tip: Consider using the various available attributes https://stackoverflow.com/a/15051390/11106111
        
            By default, configuration saves as "Config.json" in mod folder.    
            Need more config files/classes? See Configuration.cs
        */


        [DisplayName("Date Format")]
        [Description("How the date is formatted when displayed. Default: MM/dd")]
        public string DateFormat { get; set; } = "MM/dd";

        [DisplayName("Debug Mode")]
        [Description("Logs additional information to the console that is useful for debugging.")]
        public bool Debug { get; set; } = false;

        [DisplayName("Elapsed Battle Time")]
        [Description("When in battle elapsed time shows how long the battle has been going for.")]
        public bool BattleTime { get; set; } = true;

        [DisplayName("Connection Retry Delay")]
        [Description("How long the mod will wait to retry an unsuccessful rpc connection in seconds.")]
        public int RetryDelay { get; set; } = 30;
    }
}
