using System;

namespace AcadrePWI.Acadre
{
    [Serializable]
    public class AcadrePWIToken
    {
        public string AccessToken { get; set; }
        public DateTime ExpirationTime { get; set; }
        public DateTime FetchedAt { get; set; }
    }
}
