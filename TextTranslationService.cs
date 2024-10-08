﻿/*
 * Text Translation Service Facade
 */

#region Usings
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
#endregion

namespace DocumentTranslationService.Core
{

    public partial class DocumentTranslationService
    {
        #region Properties and fields

        private const int MillisecondsTimeout = 100;

        public event EventHandler RetryingEvent;

        //https://learn.microsoft.com/en-us/azure/ai-services/Translator/service-limits
        private const int maxrequestsize = 50000;   //service max request size is 50000

        public static int Maxrequestsize { get => maxrequestsize; }

        public enum ContentType { plain, HTML };
        #endregion

        #region Public Methods and Operators

        /// <summary>
        /// Detect the languages of the input
        /// </summary>
        /// <param name="input">Input string to detect the language of</param>
        /// <returns>JSON object of the detected languages</returns>
        public async Task<string> DetectAsync(string input)
        {
            string uri = TextTransUri + "/detect?api-version=3.0";
            object[] body = [new { Text = input }];
            using HttpClient client = HttpClientFactory.GetHttpClient();
            using HttpRequestMessage request = new();
            client.Timeout = TimeSpan.FromSeconds(2);
            request.Method = HttpMethod.Post;
            request.RequestUri = new Uri(uri);
            string requestBody = JsonSerializer.Serialize(body);
            request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");
            SetHeaders(request);
            HttpResponseMessage response = new();
            try
            {
                response = await client.SendAsync(request).ConfigureAwait(false);
            }
            catch (HttpRequestException e)
            {
                Debug.WriteLine("Received exception {0}", e.Message);
                return null;
            }
            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                string detectResult = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                JsonNode jsonDom = JsonNode.Parse(detectResult);
                return jsonDom[0]!["language"].ToJsonString();
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Set the request headers appropriately for the region
        /// </summary>
        /// <param name="request">Request object to set the headers for</param>
        private void SetHeaders(HttpRequestMessage request)
        {
            request.Headers.Add("Ocp-Apim-Subscription-Key", SubscriptionKey);
            if (!(AzureRegion?.ToLowerInvariant() == "global")) request.Headers.Add("Ocp-Apim-Subscription-Region", AzureRegion);
        }



        public class DetectResult
        {
            public DetectResultElement[] _ { get; set; }
        }

        /// <summary>
        /// This class is a mirror of the service's language detection response.
        /// Naming rule violation is an artifact of the service naming of the class elements.
        /// </summary>
        public class DetectResultElement
        {
            public string language { get; set; }
            public float score { get; set; }
            public bool isTranslationSupported { get; set; }
            public bool isTransliterationSupported { get; set; }
        }

        /// <summary>
        /// Test if a given category value is a valid category in the system.
        /// Works across V2 and V3 of the API.
        /// </summary>
        /// <param name="category">Category ID</param>
        /// <returns>True if the category is valid</returns>
        public async Task<bool> IsCategoryValidAsync(string category)
        {
            if (string.IsNullOrEmpty(category)) return true;
            if (category.Equals("general", StringComparison.InvariantCultureIgnoreCase)) return true;
            if (category.Equals("generalnn", StringComparison.InvariantCultureIgnoreCase)) return true;
            if (category.Equals("tech", StringComparison.InvariantCultureIgnoreCase)) return true;

            bool returnvalue = true;

            for (int i = 0; i < 3; i++)
            {
                try
                {
                    string[] teststring = ["Test"];
                    string remembercategory = Category;
                    Task<string[]> translateTask = TranslateTextAsyncInternal(teststring, "en", "he", category, ContentType.plain);
                    await translateTask.ConfigureAwait(false);
                    if (translateTask.Result == null) return false; else return true;
                }
                catch (Exception)
                {
                    returnvalue = false;
                    await Task.Delay(1000);
                    continue;
                }
            }
            return returnvalue;
        }


        /// <summary>
        /// Split a string > than <see cref="Maxrequestsize"/> into a list of smaller strings, at the appropriate sentence breaks. 
        /// </summary>
        /// <param name="text">The text to split.</param>
        /// <param name="languagecode">The language code to apply.</param>
        /// <returns>List of strings, each one smaller than maxrequestsize</returns>
        private async Task<List<string>> SplitStringAsync(string text, string languagecode)
        {
            List<string> result = [];
            int previousboundary = 0;
            if (text.Length <= Maxrequestsize)
            {
                result.Add(text);
            }
            else
            {
                while (previousboundary <= text.Length)
                {
                    int boundary = await LastSentenceBreakAsync(text[previousboundary..], languagecode).ConfigureAwait(false);
                    if (boundary == 0) break;
                    result.Add(text.Substring(previousboundary, boundary));
                    previousboundary += boundary;
                }
                result.Add(text[previousboundary..]);
            }
            return result;
        }


        /// <summary>
        /// Returns the last sentence break in the text.
        /// </summary>
        /// <param name="text">The original text</param>
        /// <param name="languagecode">A language code</param>
        /// <returns>The offset of the last sentence break, from the beginning of the text.</returns>
        private async Task<int> LastSentenceBreakAsync(string text, string languagecode)
        {
            int sum = 0;
            List<int> breakSentenceResult = await BreakSentencesInternalAsync(text, languagecode).ConfigureAwait(false);
            for (int i = 0; i < breakSentenceResult.Count - 1; i++) sum += breakSentenceResult[i];
            return sum;
        }

        /// <summary>
        /// Translates a string.
        /// </summary>
        /// <param name="text">Text to translate</param>
        /// <param name="from">From languagecode</param>
        /// <param name="to">To languagecode</param>
        /// <param name="category">Category ID</param>
        /// <param name="contentType">Plain text or HTML. Default is plain text.</param>
        /// 
        /// <returns></returns>
        public async Task<string> TranslateStringAsync(string text, string from, string to, ContentType contentType = ContentType.plain)
        {
            string[] vs = [text];
            string[] result = await TranslateTextAsync(vs, from, to, contentType);
            try
            {
                return result[0];
            }
            catch (System.IndexOutOfRangeException)
            {
                //A translate call with an expired subscription causes a null return
                throw new UnauthorizedAccessException("Invalid or expired Azure key.");
            }
        }



        // Used in the BreakSentences method.
        private class BreakSentenceResult
        {
            public int[] SentLen { get; set; }
            public DetectedLanguage DetectedLanguage { get; set; }
        }

        private class DetectedLanguage
        {
            public string Language { get; set; }
            public float Score { get; set; }
        }

        public async Task<string> Dictionary(string text, string from, string to)
        {
            string path = "/dictionary/lookup?api-version=3.0";
            string params_ = "&from=" + from + "&to=" + to;
            string uri = TextTransUri + path + params_;
            object[] body = [new { Text = text }];
            using var client = HttpClientFactory.GetHttpClient();
            using var request = new HttpRequestMessage();
            request.Method = HttpMethod.Post;
            request.RequestUri = new Uri(uri);
            request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            SetHeaders(request);
            var response = client.SendAsync(request).Result;
            var jsonResponse = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return jsonResponse;
        }

        /// <summary>
        /// Breaks a string into sentences, regardless of the length of the input text. 
        /// </summary>
        /// <param name="text">The text to split. </param>
        /// <param name="languagecode">The language of the text</param>
        /// <returns>List of integers of sentence lengths.</returns>
        public async Task<List<int>> BreakSentencesAsync(string text, string languagecode)
        {
            if (text.Length > maxrequestsize)
            {
                List<string> splits = await SplitStringAsync(text, languagecode).ConfigureAwait(false);
                List<int> resultlist = [];
                foreach (string str in splits)
                {
                    List<int> toadd = await BreakSentencesInternalAsync(str, languagecode).ConfigureAwait(false);
                    resultlist.AddRange(toadd);
                }
                return resultlist;
            }
            else return await BreakSentencesInternalAsync(text, languagecode).ConfigureAwait(false);
        }


        /// <summary>
        /// Breaks string into sentences. The string will be cut off at maxrequestsize. 
        /// </summary>
        /// <param name="text"></param>
        /// <param name="language"></param>
        /// <returns>List of integers denoting the offset of the sentence boundaries</returns>
        private async Task<List<int>> BreakSentencesInternalAsync(string text, string languagecode)
        {
            if (String.IsNullOrEmpty(text) || String.IsNullOrWhiteSpace(text)) return null;
            string path = "/breaksentence?api-version=3.0";
            string params_ = "&language=" + languagecode;
            string uri = TextTransUri + path + params_;
            object[] body = [new { Text = text[..((text.Length < Maxrequestsize) ? text.Length : Maxrequestsize)] }];
            string requestBody = JsonSerializer.Serialize(body);
            List<int> resultList = [];

            using (HttpClient client = HttpClientFactory.GetHttpClient())
            using (HttpRequestMessage request = new())
            {
                request.Method = HttpMethod.Post;
                request.RequestUri = new Uri(uri);
                request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");
                SetHeaders(request);

                HttpResponseMessage response = await client.SendAsync(request).ConfigureAwait(false);
                string result = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                BreakSentenceResult[] deserializedOutput = JsonSerializer.Deserialize<BreakSentenceResult[]>(result);
                foreach (BreakSentenceResult o in deserializedOutput)
                {
                    Debug.WriteLine("The detected language is '{0}'. Confidence is: {1}.", o.DetectedLanguage.Language, o.DetectedLanguage.Score);
                    Debug.WriteLine("The first sentence length is: {0}", o.SentLen[0]);
                    resultList = [.. o.SentLen];
                }
            }
            return resultList;
        }

        /// <summary>
        /// Translate an array of texts. An element may be larger than <see cref="Maxrequestsize"/>.
        /// </summary>
        /// <param name="texts">Array of text elements</param>
        /// <param name="from">From language</param>
        /// <param name="to">To language</param>
        /// <param name="category">Category ID from Custom Translator</param>
        /// <param name="contentType">Plain text or HTML</param>
        /// <param name="retrycount">How many times to retry</param>
        /// <returns></returns>
        private async Task<string[]> TranslateTextAsync(string[] texts, string from, string to, ContentType contentType = ContentType.plain)
        {
            if (from == to) return texts;
            bool translateindividually = false;
            foreach (string text in texts)
            {
                if (text.Length >= Maxrequestsize) translateindividually = true;
            }
            if (translateindividually)
            {
                List<string> resultlist = [];
                foreach (string text in texts)
                {
                    List<string> splitstring = await SplitStringAsync(text, from).ConfigureAwait(false);
                    string linetranslation = string.Empty;
                    foreach (string innertext in splitstring)
                    {
                        string[] str = [innertext];
                        string[] innertranslation = await TranslateTextAsyncInternal(str, from, to, Category, contentType).ConfigureAwait(false);
                        linetranslation += innertranslation[0];
                    }
                    resultlist.Add(linetranslation);
                }
                return [.. resultlist];
            }
            else
            {
                return await TranslateTextAsyncInternal(texts, from, to, Category, contentType).ConfigureAwait(false);
            }
        }


        /// <summary>
        /// Raw function to translate an array of strings. Does not allow elements to be larger than <see cref="Maxrequestsize"/>.
        /// </summary>
        /// <param name="texts"></param>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <param name="category"></param>
        /// <param name="contentType"></param>
        /// <param name="retrycount"></param>
        /// <returns></returns>
        private async Task<string[]> TranslateTextAsyncInternal(
            string[] texts,
            string from,
            string to,
            string category,
            ContentType contentType,
            int retrycount = 3)
        {
            string path = "/translate?api-version=3.0";
            if (ShowExperimental) path += "&flight=experimental";
            string params_ = "&from=" + from + "&to=" + to;
            string thiscategory = category;
            if (String.IsNullOrEmpty(category))
            {
                thiscategory = null;
            }
            else
            {
                if (thiscategory == "generalnn") thiscategory = null;
                if (thiscategory == "general") thiscategory = null;
            }
            if (thiscategory != null) params_ += "&category=" + System.Web.HttpUtility.UrlEncode(category);
            if (contentType == ContentType.HTML) params_ += "&textType=HTML";
            string uri = TextTransUri + path + params_;


            ArrayList requestAL = [];
            foreach (string text in texts)
            {
                requestAL.Add(new { Text = text });
            }
            string requestJson = JsonSerializer.Serialize(requestAL);

            IList<string> resultList = [];
            while (retrycount > 0)
            {
                var client = HttpClientFactory.GetHttpClient();
                var request = new HttpRequestMessage();
                client.Timeout = TimeSpan.FromSeconds(20);
                request.Method = HttpMethod.Post;
                request.RequestUri = new Uri(uri);
                request.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");
                SetHeaders(request);

                HttpResponseMessage response = new()
                {
                    StatusCode = System.Net.HttpStatusCode.RequestTimeout
                };
                string responseBody = string.Empty;

                try
                {
                    response = await client.SendAsync(request).ConfigureAwait(false);
                }
                catch (TaskCanceledException)
                {
                    response.StatusCode = System.Net.HttpStatusCode.RequestTimeout;
                    await Task.Delay(MillisecondsTimeout);
                    request.Dispose(); client.Dispose(); response.Dispose();
                    continue;
                }
                int status = (int)response.StatusCode;
                if (status != 200) Debug.WriteLine($"TranslateText error: {response.ReasonPhrase}");
                switch (status)
                {
                    case 200:
                        break;
                    case 400: throw new InvalidCategoryException(category);
                    case 401: throw new AccessViolationException("Invalid credentials. Check for key/region mismatch.");
                    case 408:       //Custom system is being loaded
                        Debug.WriteLine("Retry #" + retrycount + " Response: " + (int)response.StatusCode);
                        await Task.Delay(MillisecondsTimeout * 10);
                        RetryingEvent?.Invoke(null, EventArgs.Empty);
                        request.Dispose(); client.Dispose(); response.Dispose();
                        continue;
                    case 429:
                    case 500:
                    case 503:       //translate the array one element at a time
                        if (texts.Length > 1)
                        {
                            for (int i = 0; i < texts.Length; i++)
                            {
                                try
                                {
                                    string[] totranslate = [texts[i]];
                                    string[] result = new string[1];
                                    result = await TranslateTextAsyncInternal(totranslate, from, to, category, contentType, 2).ConfigureAwait(false);
                                    resultList.Add(result[0]);
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine("Failed to translate: {0}\n{1}", texts[i], ex.Message);
                                    resultList.Add(texts[i]);
                                }
                            }
                            return [.. resultList];
                        }
                        else
                        {
                            Debug.WriteLine("Retry #" + retrycount + " Response: " + (int)response.StatusCode);
                            await Task.Delay(MillisecondsTimeout);
                            request.Dispose(); client.Dispose(); response.Dispose();
                            continue;
                        }
                    default:
                        await Task.Delay(MillisecondsTimeout * 5);
                        retrycount--;
                        request.Dispose(); client.Dispose(); response.Dispose();
                        continue;
                }
                responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                request.Dispose(); client.Dispose(); response.Dispose();
                if (!string.IsNullOrEmpty(responseBody))
                {
                    JsonDocument jaresult;
                    try
                    {
                        jaresult = JsonDocument.Parse(responseBody);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("{0}\n{1}", responseBody, ex.Message);
                        throw;
                    }
                    var translations = jaresult.RootElement.EnumerateArray();
                    foreach (var trans in translations)
                    {
                        var item = trans.GetProperty("translations");
                        foreach (var line in item.EnumerateArray())
                        {
                            var txt = line.GetProperty("text");
                            resultList.Add(txt.GetString());
                        }
                    }
                }
                break;
            }

            return [.. resultList];
        }
        #endregion
    }
}