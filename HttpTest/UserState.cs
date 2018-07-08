using Newtonsoft.Json.Linq;
using System;
using System.Net;

namespace HttpTest
{
    [Serializable]
    public class UserState
    {
        public string Password { get ; set ; }
        public string UserName { get ; set ; }
        public string CurrentAlias { get; set; }
        public bool Result { get; set; }
        public string ResponseText { get ; set ; }
        public string ErrorMessage { get; set ; }
        public RequestStatusEnum RequestStatus { get ; set ; }

        public string SessionId { get; set; }
        public string RequestVerificationToken { get; set; }
        public string UserToken { get; set; }
        public string MyKFedAuth { get; set; }

        public WebHeaderCollection RequestHeaders { get ; set ; }
        public WebHeaderCollection ResponseHeaders { get;  set ; }

        public string ResponseStatus { get; set; }
        public HttpStatusCode ResponseStatusCode { get; set; }

        public CookieCollection Cookie { get; set; }

        public UserState() : this(string.Empty, string.Empty) { }           

        public UserState(string userName, string password)
        {
            UserName = userName;
            Password = password;
            Cookie = new CookieCollection();
            RequestHeaders = new WebHeaderCollection();
        }
    }
}
