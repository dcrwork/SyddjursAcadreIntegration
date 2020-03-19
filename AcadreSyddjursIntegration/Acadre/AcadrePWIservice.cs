using System;
using System.Globalization;
using System.Net.Http;
using System.Threading.Tasks;
using RestSharp;
using Newtonsoft.Json;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using System.Linq;
using System.Collections;
using System.Text.RegularExpressions;
using AcadreLib;

namespace AcadrePWI.Acadre
{
    public class AcadrePWIservice
    {
        private System.Net.Cookie cachedCookie;
        private AcadrePWIToken acadreToken;
        private RestClient restClientAuth;
        private RestClient restClient;
        private string CaseFileTypeCode;
        public AcadrePWIservice(string AcadreBaseURL,string AcadreUserName, string AcadreUserPassword)
        {
            restClientAuth = new RestClient(AcadreBaseURL)
            {
                Authenticator = new RestSharp.Authenticators.NtlmAuthenticator(AcadreUserName, AcadreUserPassword)
            };
            restClient = new RestClient(AcadreBaseURL);
            CaseFileTypeCode = "BUSAG";
        }

        public void GetCookie()
        {
            if (cachedCookie != null && !(cachedCookie.Expired || DateTime.Now > cachedCookie.TimeStamp.AddHours(4)))
            {
                return;
            }
            var request = new RestRequest("/Frontend/AuthService/GetLoginUrl", Method.GET);
            request.UseDefaultCredentials = false;
            var LoginURL = restClientAuth.Execute(request).Content;
            request = new RestRequest(Method.GET);
            restClientAuth.BaseUrl = new Uri(LoginURL);
            var response = restClientAuth.Execute(request);

            var AuthCookie = response.Cookies[0];
            cachedCookie = new System.Net.Cookie() { Domain = AuthCookie.Domain, Expires = AuthCookie.Expires, Value = AuthCookie.Value, Comment = AuthCookie.Comment, CommentUri = AuthCookie.CommentUri, Discard = AuthCookie.Discard, Expired = AuthCookie.Expired, HttpOnly = AuthCookie.HttpOnly, Port = AuthCookie.Port, Secure = AuthCookie.Secure, Version = AuthCookie.Version, Name = AuthCookie.Name };
            //restClientAuth.CookieContainer = new System.Net.CookieContainer();
            //restClientAuth.CookieContainer.Add(cachedCookie); //
            //acadreToken = new AcadreToken
            //{
            //    AccessToken = response.Cookies[0].Value,
            //    ExpirationTime = response.Cookies[0].Expires,
            //    FetchedAt = DateTime.Now
            //};
            //return acadreToken;
            //return cachedCookie;
        }

        public IEnumerable<AcadrePWIContact> SearchContacts(string SearchTerm)
        {
            List<AcadrePWIContact> acadrePWIContacts = new List<AcadrePWIContact>();
            var request = new RestRequest("/Frontend/api/v8/contact", Method.GET);
            request.RequestFormat = DataFormat.Json;
            request.AddParameter("SearchKey", SearchTerm);
            request.AddParameter("page-size", 100);
            int i = 0;
            while (true)
            {
                request.AddOrUpdateParameter("page-index", i);
                var response = JsonConvert.DeserializeObject<AcadrePWIContact[]>(Execute(request).Content);
                acadrePWIContacts.AddRange(response);
                if (response.Length < 100)
                    break;
                i++;
                if (acadrePWIContacts.Count() == 500)
                    break;
            }
            return acadrePWIContacts;
        }

        public IEnumerable<AcadrePWIcase> SearchCases(SearchCriterion searchCriterion)
        {
            List<AcadrePWIcase> acadrePWIcases = new List<AcadrePWIcase>();
            var request = new RestRequest("/Frontend/api/v8/case", Method.GET);
            request.RequestFormat = DataFormat.Json;
            request.AddParameter("SearchCaseType", CaseFileTypeCode);
            if (searchCriterion.IsClosed.HasValue)
                request.AddParameter("Active", !searchCriterion.IsClosed);
            request.AddParameter("page-size", 100);
            if(searchCriterion.PrimaryContactsName != "")
                request.AddParameter("ContactSearchKey", searchCriterion.PrimaryContactsName);
            request.AddParameter("SearchTerm", searchCriterion.CaseContent);
            int i = 0;
            while(true)
            {
                request.AddOrUpdateParameter("page-index", i);
                var response = Execute(request).Content;
                var acadrePWIcasesresponse = JsonConvert.DeserializeObject<AcadrePWIcase[]>(response);
                acadrePWIcases.AddRange(acadrePWIcasesresponse);
                if (acadrePWIcasesresponse.Length < 100)
                    break;
                i++;
            }
            return acadrePWIcases.Where(x => (x.ResponsibleUserName == searchCriterion.CaseManagerInitials || searchCriterion.CaseManagerInitials == null) /*&& (searchCriterion.PrimaryContactsName.Replace("*","").Split(' ').Where(y=>x.Description.Contains(y)).Any() || searchCriterion.PrimaryContactsName == "")*/ && (searchCriterion.AcadreOrgID == x.ResponsibleUnit.Id || searchCriterion.AcadreOrgID == 0) && (searchCriterion.ChildCPR == x.Title || searchCriterion.ChildCPR == "*") && (searchCriterion.KLE == null /*|| searchCriterion.KLE = x.Classification*/));
        }
        public IRestResponse Execute(RestRequest restRequest)
        {
            GetCookie();
            restRequest.AddOrUpdateParameter(cachedCookie.Name, cachedCookie.Value, ParameterType.Cookie);
            return restClient.Execute(restRequest);
        }
        

    }

}
