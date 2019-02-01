//    Copyright 2018 athenahealth, Inc.
//
//   Licensed under the Apache License, Version 2.0 (the "License"); you
//   may not use this file except in compliance with the License.  You
//   may obtain a copy of the License at
//
//       http://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or
//   implied.  See the License for the specific language governing
//   permissions and limitations under the License.

using System;
using System.Net;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;

using Newtonsoft.Json;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace Athenahealth
{
    public class Naive
    {

        static public void Main()
        {
            try
            {
                RunAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
                Console.WriteLine(ex.StackTrace);

            }
        }

        static async Task RunAsync()
        {
            MDPRequests mdp = new MDPRequests();
            mdp.GetPractices();
            mdp.GetDepartments();
            // default appointment gets a 404
            //mdp.SetAppointmentNote(token);

            await mdp.GetCarePlan();

        }
    }

    public class MDPRequests
    {

        // Set everything up
        string version = "preview1";
        string starterPractice = "1";

        // Patient I created in MDP Ambulatory (Practice: 195900 Dept: 102)
        string patientId = "34718";

        private string token = null;

        Encoding UTF8 = System.Text.Encoding.GetEncoding("utf-8");

        Dictionary<string, string> previewPractices = new Dictionary<string, string>();

        // Easier to keep track of OAuth prefixes
        Dictionary<string, string> auth_prefixes = new Dictionary<string, string>()
      {
        {"v1", "/oauth"},
        {"preview1", "/oauthpreview"},
        {"openpreview1", "/oauthopenpreview"},
      };

      UriBuilder baseUrl = Config.MDPUrl;

        public Uri MDPUri
        {
            get { return Config.MDPUrl.Uri; }
        }
        public string Token
        {
            get
            {
                if (token != null)
                    return token;

                Dictionary<string, string> parameters = new Dictionary<string, string>()
              {
                      {"grant_type", "client_credentials"},
              };

                // Create and set up a request
                string tokenPath = PathJoin(auth_prefixes[version], "/token");
                baseUrl.Path = tokenPath;
                Console.WriteLine("Calling " + baseUrl);
                WebRequest request = WebRequest.Create(baseUrl.Uri);
                request.Method = "POST";
                request.ContentType = "application/x-www-form-urlencoded";

                // Make sure to add the Authorization header
                string auth = System.Convert.ToBase64String(UTF8.GetBytes(Config.MDPKey + ":" + Config.MDPSecret));
                request.Headers["Authorization"] = "Basic " + auth;

                // Encode the parameters, convert it to bytes (because that's how the streams want it)
                string encoded = UrlEncode(parameters);
                byte[] content = UTF8.GetBytes(encoded);

                // Write the parameters to the body
                Stream writer = request.GetRequestStream();
                writer.Write(content, 0, content.Length);
                writer.Close();

                // Get the response, read it out, and decode it
                WebResponse response = request.GetResponse();
                Stream receive = response.GetResponseStream();
                StreamReader reader = new StreamReader(receive, UTF8);
                dynamic authorization = JsonConvert.DeserializeObject(reader.ReadToEnd());

                // Make sure to grab the token!
                token = authorization["access_token"];
                Console.WriteLine(token);

                // And always remember to close the readers and streams
                response.Close();
                return token;

            }
        }

        ///
        /// /v1/{practiceid}/ccda/{patientid}/patientcareplan
        ///
        public async Task GetCarePlan()
        {
            string carePlanPath = PathJoin(version, starterPractice, "ccda", patientId, "patientcareplan");
            using (var client = new HttpClient())
            {
                client.BaseAddress = MDPUri;
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);

                try
                {
                    var content = await client.GetStringAsync(carePlanPath);
                    Console.WriteLine(JsonConvert.DeserializeObject(content));
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine("Error getting Care Plan: " + ex.Message);
                }

            }
        }

        /// <summary>
        /// GET /practiceinfo
        /// </summary>
        public void GetPractices()
        {
            string practicePath = PathJoin(version, starterPractice, "practiceinfo");
            baseUrl.Path = practicePath;

            Console.WriteLine("Calling " + baseUrl);
            // Create the request, add in the auth header
            WebRequest request = WebRequest.Create(baseUrl.Uri);
            request.Method = "GET";
            request.Headers["Authorization"] = "Bearer " + Token;


            // Get the response, read and decode
            WebResponse response = request.GetResponse();
            Stream receive = response.GetResponseStream();
            StreamReader reader = new StreamReader(receive, UTF8);
            //JsonObject practices = (JsonObject) JsonValue.Parse(reader.ReadToEnd());
            dynamic practices = JsonConvert.DeserializeObject(reader.ReadToEnd());
            response.Close();

            foreach (Newtonsoft.Json.Linq.JObject o in practices.practiceinfo)
            {
                string id = (string)o["practiceid"];
                string name = (string)o["name"];
                previewPractices.Add(id, name);
                //Console.WriteLine("Practice Id: " + id + " Name: " + name);
            }
        }

        /// <summary>
        /// GET /departments per practice
        /// </summary>
        public void GetDepartments()
        {

            // Since GET parameters go in the URL, we set up the parameters Dictionary first
            Dictionary<string, string> parameters = new Dictionary<string, string>()
                {
                    {"limit", "15"},
                };

            foreach (var id in previewPractices.Keys)
            {
                // Now we get to make the URL, making sure to encode the parameters and remember the "?"
                string deptPath = PathJoin(version, id, "/departments");
                baseUrl.Path = deptPath;
                baseUrl.Query = UrlEncode(parameters);

                Console.WriteLine("Calling " + baseUrl);
                // Create the request, add in the auth header
                WebRequest request = WebRequest.Create(baseUrl.Uri);
                request.Method = "GET";
                request.Headers["Authorization"] = "Bearer " + Token;

                // Get the response, read and decode
                WebResponse response = request.GetResponse();
                Stream receive = response.GetResponseStream();
                StreamReader reader = new StreamReader(receive, UTF8);
                dynamic departments = JsonConvert.DeserializeObject(reader.ReadToEnd());
                Console.WriteLine(departments.ToString());

                response.Close();

            }
        }
        public void SetAppointmentNote()
        {
            // POST /appointments/{appointmentid}/notes
            string practice = previewPractices.Keys.First();
            Console.WriteLine("Attempting to set appointment for practice " + practice);

            baseUrl.Path = PathJoin(version, practice, "/appointments/1/notes");
            string noteText = "Hello from C# - TRT " + DateTime.Now;
            Dictionary<string, string> parameters = new Dictionary<string, string>()
          {
            {"notetext", noteText},
          };

            WebRequest request = WebRequest.Create(baseUrl.Uri);
            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded";
            request.Headers["Authorization"] = "Bearer " + Token;

            byte[] content = UTF8.GetBytes(UrlEncode(parameters));
            Stream writer = request.GetRequestStream();
            writer.Write(content, 0, content.Length);
            writer.Close();

            WebResponse response = request.GetResponse();
            Stream receive = response.GetResponseStream();
            StreamReader reader = new StreamReader(receive, UTF8);
            dynamic note = JsonConvert.DeserializeObject(reader.ReadToEnd());
            Console.WriteLine(note.ToString());

            response.Close();

        }

        // A useful function for encoding parameters into query strings
        private string UrlEncode(Dictionary<string, string> dict)
        {
            return string.Join("&", dict.Select(
              kvp => WebUtility.UrlEncode(kvp.Key) + "=" + WebUtility.UrlEncode(kvp.Value)
            ).ToList());
        }

        // A useful function for joining paths into URLs
        private string PathJoin(params string[] args)
        {
            return string.Join("/", args
                               .Select(arg => arg.Trim(new char[] { '/' }))
                               .Where(arg => !String.IsNullOrEmpty(arg))
            );
        }

    }
}

