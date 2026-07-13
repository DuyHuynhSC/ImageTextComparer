using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ImageTextComparer
{
    public static class VisionApiService
    {
        public static bool BypassSslValidation { get; set; } = false;

        private static readonly HttpClient _httpClient = new HttpClient(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) =>
            {
                if (BypassSslValidation) return true;
                return sslPolicyErrors == System.Net.Security.SslPolicyErrors.None;
            }
        });

        /// <summary>
        /// Sends an image to the OpenAI-compatible Vision API and returns the extracted text.
        /// </summary>
        public static async Task<string> ExtractTextAsync(string endpoint, string apiKey, string modelName, string prompt, byte[] imageBytes)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
                throw new ArgumentException("API Endpoint URL cannot be empty.", nameof(endpoint));

            string base64Image = Convert.ToBase64String(imageBytes);

            // Construct payload following OpenAI Chat Completions Vision specification
            var payload = new
            {
                model = modelName,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new { type = "text", text = prompt },
                            new { type = "image_url", image_url = new { url = $"data:image/png;base64,{base64Image}" } }
                        }
                    }
                }
            };

            string jsonPayload = JsonSerializer.Serialize(payload);

            using (var request = new HttpRequestMessage(HttpMethod.Post, endpoint))
            {
                request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                if (!string.IsNullOrWhiteSpace(apiKey))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                }

                // Add standard headers just in case
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                using (var response = await _httpClient.SendAsync(request))
                {
                    string responseContent = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        // Check if it is an HTTP redirect (301, 302, 307, 308)
                        if (response.StatusCode == System.Net.HttpStatusCode.MovedPermanently || // 301
                            response.StatusCode == System.Net.HttpStatusCode.Found || // 302
                            response.StatusCode == System.Net.HttpStatusCode.SeeOther || // 303
                            response.StatusCode == System.Net.HttpStatusCode.TemporaryRedirect || // 307
                            (int)response.StatusCode == 308) // Permanent Redirect
                        {
                            var redirectUri = response.Headers.Location;
                            if (redirectUri != null)
                            {
                                // Make relative redirect URIs absolute if needed
                                Uri absoluteUri = redirectUri.IsAbsoluteUri ? redirectUri : new Uri(new Uri(endpoint), redirectUri);
                                throw new Exception($"Địa chỉ API Endpoint của bạn đã được chuyển hướng (Redirect {(int)response.StatusCode}) sang địa chỉ mới:\n--> {absoluteUri}\n\nVui lòng copy địa chỉ mới này dán lại vào ô 'API Endpoint URL' ở cột cấu hình bên trái và thử lại.");
                            }
                        }

                        throw new Exception($"API Request failed with status code {response.StatusCode}.\nDetails: {responseContent}");
                    }

                    try
                    {
                        using (var doc = JsonDocument.Parse(responseContent))
                        {
                            var root = doc.RootElement;
                            if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                            {
                                var choice = choices[0];
                                if (choice.TryGetProperty("message", out var message) && message.TryGetProperty("content", out var content))
                                {
                                    return content.GetString() ?? string.Empty;
                                }
                            }
                            
                            // If it's a non-standard OpenAI structure but contains some text, let's try to extract it
                            return responseContent;
                        }
                    }
                    catch (JsonException)
                    {
                        // Fallback if the response isn't JSON
                        return responseContent;
                    }
                }
            }
        }
    }
}
