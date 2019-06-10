using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;

namespace CloudFlareBypass
{
    public class CfHttpClient : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _userAgent;
        private readonly CookieContainer _cc = new CookieContainer();

        public CfHttpClient(string userAgent, IWebProxy proxy)
        {
            var handler = new HttpClientHandler()
            {
                AllowAutoRedirect = false,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                CookieContainer = _cc,
                UseProxy = proxy != null,
                Proxy = proxy
            };

            _httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(20)
            };

            _userAgent = userAgent;
        }

        public async Task<KeyValuePair<HttpStatusCode, string>> GetHtmlPage(string url, Dictionary<string, string> aditionalHeaders = null)
        {
            return await GetHtmlPageInternal(url, aditionalHeaders);
        }

        private async Task<KeyValuePair<HttpStatusCode, string>> GetHtmlPageInternal(string url, Dictionary<string, string> aditionalHeaders = null, bool recursive = true)
        {
            try
            {
                var requestMessage = new HttpRequestMessage(HttpMethod.Get, url.Trim());

                requestMessage.Headers.Add("User-Agent", _userAgent);
                requestMessage.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
                requestMessage.Headers.Add("Accept-Encoding", "gzip, deflate");
                requestMessage.Headers.Add("Accept-Language", "pt-BR,pt;q=0.8,en-US;q=0.5,en;q=0.3");
                requestMessage.Headers.Add("Upgrade-Insecure-Requests", "1");
                requestMessage.Headers.Add("DNT", "1");

                if (aditionalHeaders != null)
                {
                    foreach (var header in aditionalHeaders)
                    {
                        if (requestMessage.Headers.Contains(header.Key))
                        {
                            requestMessage.Headers.Remove(header.Key);
                        }

                        requestMessage.Headers.Add(header.Key, header.Value);
                    }
                }

                var response = await _httpClient.SendAsync(requestMessage);

                var responseAsString = await response.Content.ReadAsStringAsync();

                if (recursive && CfHelper.IsChalangeresponse(response.StatusCode, responseAsString))
                {
                    // dealy 4 sec respecting CloudFlare chalange rules
                    await Task.Delay(4000);

                    var chalangeRequestUrl = CfHelper.GetAuthorizationString(responseAsString, url);

                    if (string.IsNullOrWhiteSpace(chalangeRequestUrl))
                    {
                        return new KeyValuePair<HttpStatusCode, string>(HttpStatusCode.SeeOther, "Fail to resolve chalange.");
                    }
                    else
                    {
                        // get the chaslange page passing the response of chalange in prameters (no recursion)
                        var chalangeResponse = await GetHtmlPageInternal(chalangeRequestUrl, aditionalHeaders, false);

                        var cookies = GetAllCookies(_cc);

                        var success = false;

                        // check for cf_clearance cookie, this cookie indicate the success of chalange
                        foreach (Cookie cookie in cookies)
                        {
                            if (cookie.Name == "cf_clearance" && cookie.Expired == false)
                            {
                                success = true;
                                break;
                            }
                        }

                        if (success)
                        {
                            return await GetHtmlPageInternal(url, aditionalHeaders, false);
                        }
                        else
                        {
                            return new KeyValuePair<HttpStatusCode, string>(HttpStatusCode.SeeOther, chalangeResponse.Value);
                        }
                    }
                }

                return new KeyValuePair<HttpStatusCode, string>(response.StatusCode, responseAsString);
            }
            catch (Exception ex)
            {
                return new KeyValuePair<HttpStatusCode, string>(HttpStatusCode.SeeOther, ex.Message);
            }
        }

        private static CookieCollection GetAllCookies(CookieContainer cookieJar)
        {
            var cookieCollection = new CookieCollection();

            var table = (Hashtable)cookieJar.GetType().InvokeMember("m_domainTable",
                BindingFlags.NonPublic |
                BindingFlags.GetField |
                BindingFlags.Instance,
                null,
                cookieJar,
                new object[] { });

            foreach (var tableKey in table.Keys)
            {
                var strTableKey = (string)tableKey;

                if (strTableKey[0] == '.')
                {
                    strTableKey = strTableKey.Substring(1);
                }

                var list = (SortedList)table[tableKey].GetType().InvokeMember("m_list",
                    BindingFlags.NonPublic |
                    BindingFlags.GetField |
                    BindingFlags.Instance,
                    null,
                    table[tableKey],
                    new object[] { });

                foreach (var listKey in list.Keys)
                {
                    var url = "https://" + strTableKey + (string)listKey;
                    cookieCollection.Add(cookieJar.GetCookies(new Uri(url)));
                }
            }

            return cookieCollection;
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
