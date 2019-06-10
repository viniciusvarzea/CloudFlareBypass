using System;
using System.Linq;
using System.Net;
using System.Web;
using HtmlAgilityPack;
using Jint;

namespace CloudFlareBypass
{
    public static class CfHelper
    {
        public static string GetAuthorizationString(string rawHtml, string urlRequested)
        {
            try
            {
                var urlHost = new Uri(urlRequested);

                // auto close tags when it is needed
                HtmlNode.ElementsFlags.Remove("form");
                HtmlNode.ElementsFlags.Remove("script");

                var html = new HtmlDocument();

                html.LoadHtml(rawHtml);

                // if chalange form doesn't exists, return null
                if (html.GetElementbyId("challenge-form") == null)
                {
                    return null;
                }

                // search for script node
                var node = html.DocumentNode.Descendants("head").ElementAt(0)?.Descendants("script").ElementAt(0)?.InnerHtml?.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

                // script node not found, returning null
                if (node == null)
                {
                    return null;
                }

                // here we get the value of 'K' variable in CloudFlare script
                // the value of 'K' is a name of a div that contains some JS logic for eval
                var kFieldName = node[25].Split('\'')[1];

                // here we are going to get the value in 'K' field
                var kFieldValue = html.GetElementbyId(kFieldName).InnerHtml;

                // We have to overload getElementById because JINT don't have DOM manipulation capabilities
                var js = $"var document = new Object(); document.getElementById = function(k){{ var obj = new Object(); obj.innerHTML = '{kFieldValue}'; return obj; }};";

                // join some parts of script for be interpreted by JINT
                for (var i = 7; i <= 21; i++)
                {
                    js += node[i];
                }

                var finalJs = js.Replace("t,", $"t='{urlHost.Host}',") // manual atribuition of 't' variable bacause JINT don't have DOM manipulation
                             + node[28].Replace("Function(\"return escape\")()((\"\")[\"italics\"]())[2]", "'C'") // fix JINT JS problem
                                 .Replace("a.value", "a") // transform 'a' in a string based variable
                                 .Replace("'; 121'", "").Trim().Substring(1); // remove final trash, initial ';' and trim string


                // execute the generated script and get the result stored in 'a' variable
                var jschlAnswer = new Engine().Execute(finalJs).GetValue("a");

                var s = html.GetElementbyId("challenge-form").Descendants("input").ElementAt(0).Attributes["value"].Value;
                var jschlVc = html.GetElementbyId("challenge-form").Descendants("input").ElementAt(1).Attributes["value"].Value;
                var pass = html.GetElementbyId("challenge-form").Descendants("input").ElementAt(2).Attributes["value"].Value;

                var url = urlRequested + $"/cdn-cgi/l/chk_jschl?s={HttpUtility.UrlEncode(s)}&jschl_vc=" + HttpUtility.UrlEncode(jschlVc) + "&pass=" + HttpUtility.UrlEncode(pass) + "&jschl_answer=" + HttpUtility.UrlEncode(jschlAnswer.ToString());

                return url;
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        public static bool IsChalangeresponse(HttpStatusCode statusCode, string rawHtml)
        {
            return statusCode == HttpStatusCode.ServiceUnavailable && rawHtml.Contains("challenge-form");
        }
    }
}
