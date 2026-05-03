using System;

namespace PotopopiCamSync.Models
{
    public class ImmichAccount
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "Default Account";
        public string Url { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;

        public override string ToString() => Name;
    }
}
