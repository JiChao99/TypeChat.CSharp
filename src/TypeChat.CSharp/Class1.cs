using System;
using System.Net;
using System.Text;
using System.Text.Json;

namespace TypeChat
{
    public interface ITypeChatJsonTranslator<T> where T : class
    {
        /// <summary>
        /// create request prompt
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        string CreateRequestPrompt(string request);

        /// <summary>
        /// create repair prompt
        /// </summary>
        /// <param name="validationError">error text</param>
        /// <returns></returns>
        string CreateRepairPrompt(string validationError);

        /// <summary>
        /// translate request to object
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        Task<Result<T>> Translate(HttpClient httpClient, string request);
    }

    /// <summary>
    /// Represents the type of API service.
    /// </summary>
    public enum APIServiceType
    {
        None = 0,
        Azure = 1,
        OpenAI = 2
    }

    /// <summary>
    /// Represents the configuration for an API service.
    /// </summary>
    public class TypeChatAPIServiceConfig
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TypeChatAPIServiceConfig"/> class.
        /// set all property
        /// </summary>
        public TypeChatAPIServiceConfig(APIServiceType type, string endpoint, string apiKey, string? deploymentName = null, string? modelId = null)
        {
            Type = type;
            Endpoint = endpoint;
            ApiKey = apiKey;
            DeploymentName = deploymentName ?? string.Empty;
            ModelId = modelId ?? string.Empty;
        }

        /// <summary>
        /// the type of the service.
        /// </summary>
        public APIServiceType Type { get; set; }

        /// <summary>
        /// the deployment name for Azure service.
        /// </summary>
        public string DeploymentName { get; set; } = string.Empty;

        /// <summary>
        /// the model ID for OpenAI service.
        /// </summary>
        /// <example>
        /// gpt-3.5-turbo or gpt-4
        /// </example>
        public string ModelId { get; set; } = string.Empty;

        /// <summary>
        /// the API endpoint.
        /// </summary>
        /// <example>
        /// openai https://api.openai.com/v1/chat/completions
        /// azure https://YOUR_RESOURCE_NAME.openai.azure.com/openai/deployments/YOUR_DEPLOYMENT_NAME/chat/completions?api-version=2023-05-15
        public string Endpoint { get; set; } = string.Empty;

        /// <summary>
        /// the API key for authentication.
        /// </summary>
        public string ApiKey { get; set; } = string.Empty;
    }

    public class TypeChatJsonTranslator<T> : ITypeChatJsonTranslator<T> where T : class
    {
        private string Schema { get; init; }
        private string Request { get; init; }
        private int MaxDeep { get; init; }

        private int RetryMaxAttempts { get; init; }

        private int RetryPauseMs { get; init; }
        public bool AttemptRepair { get; set; }
        public TypeChatJsonTranslator(string request, string? schema = null, int maxDeep = 2, bool attemptRepair = true, int retryMaxAttempts = 3, int retryPauseMs = 1000)
        {
            Request = request;
            Schema = !string.IsNullOrEmpty(schema) ? schema : GetSchema(typeof(T));
            MaxDeep = maxDeep;
            AttemptRepair = attemptRepair;
            RetryMaxAttempts = retryMaxAttempts;
            RetryPauseMs = retryPauseMs;
        }
        public string CreateRepairPrompt(string validationError) =>
            $"The JSON object is invalid for the following reason:\n" +
            $"\"\"\"\n{validationError}\n\"\"\"\n" +
            $"The following is a revised JSON object:\n";

        public string CreateRequestPrompt(string request) =>
            $"You are a service that translates user requests into JSON objects of type \"{nameof(T)}\" according to the following TypeScript definitions:\n" +
            $"```\n{Schema}\n```\n" +
            $"The following is a user request:\n" +
            $"\"\"\"\n{Request}\n\"\"\"\n" +
            $"The following is the user request translated into a JSON object with 2 spaces of indentation and no properties with the value undefined:\n";

        public async Task<Result<T>> Translate(HttpClient httpClient, string request)
        {
            string prompt = CreateRequestPrompt(request);
            bool attemptRepair = AttemptRepair;

            while (true)
            {
                var response = await Complete(httpClient, prompt);
                if (!response.IsSuccess)
                {
                    return new Result<T> { IsSuccess = false, ErrorMessage = response.ErrorMessage };
                }

                string responseText = response.Data ?? "";
                int startIndex = responseText.IndexOf("{");
                int endIndex = responseText.LastIndexOf("}");

                if (startIndex >= 0 && endIndex > startIndex)
                {
                    string jsonText = responseText.Substring(startIndex, endIndex - startIndex + 1);

                    try
                    {
                        var result = JsonSerializer.Deserialize<T>(jsonText);
                        return new Result<T>
                        {
                            IsSuccess = true,
                            Data = result,

                        };
                    }
                    catch (JsonException ex)
                    {
                        if (!attemptRepair)
                        {
                            return new Result<T> { IsSuccess = false, ErrorMessage = $"JSON validation failed: {ex.Message}\n{jsonText}" };
                        }
                        prompt += $"{responseText}\n{CreateRepairPrompt(ex.Message)}";
                        AttemptRepair = false;
                    }
                }
                else
                {
                    return new Result<T> { IsSuccess = false, ErrorMessage = $"Response is not JSON:\n{responseText}" };
                }
            }
        }
        public async Task<Result<string>> Complete(HttpClient httpClient, string prompt)
        {
            int retryCount = 0;

            while (true)
            {
                Dictionary<string, string> paramsDict = new Dictionary<string, string>()
            {
                { "messages", "[{\"role\": \"user\", \"content\": prompt}]"},
                { "temperature", "0"},
                { "n", "1"}
                };


                var result = await httpClient.PostAsync(url, new FormUrlEncodedContent(paramsDict));

                if (result.IsSuccessStatusCode)
                {
                    // Process the success response
                    return new Result<string> { IsSuccess = true, Data = await result.Content.ReadAsStringAsync() };
                }

                if (!IsTransientHttpError(result.StatusCode) || retryCount >= RetryMaxAttempts)
                {
                    // Process the error response
                    return new Result<string> { IsSuccess = false, ErrorMessage = $"REST API error {result.StatusCode}: {result.ReasonPhrase}" };
                }

                if (RetryPauseMs > 0)
                {
                    await Sleep(RetryPauseMs);
                }

                retryCount++;
            }
        }

        private bool IsTransientHttpError(HttpStatusCode code) => code switch
        {
            HttpStatusCode.TooManyRequests or
            HttpStatusCode.InternalServerError or
            HttpStatusCode.BadGateway or
            HttpStatusCode.ServiceUnavailable or
            HttpStatusCode.GatewayTimeout => true,
            _ => false,
        };

        private void ConfigHttpClient(HttpClient httpClient, TypeChatAPIServiceConfig config)
        {
            switch (config.Type)
            {
                case APIServiceType.Azure:
                    httpClient.BaseAddress = new Uri(config.Endpoint);
                    httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {config.ApiKey}");
                    break;
                case APIServiceType.OpenAI:
                    httpClient.BaseAddress = new Uri(config.Endpoint);
                    httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {config.ApiKey}");
                    break;
                default:
                    throw new ArgumentException($"Invalid API service type: {config.Type}");
            }

        }

        private async Task Sleep(int ms)
        {
            await Task.Delay(ms);
        }
        private string GetSchema(Type type)
        {
            var properties = type.GetProperties();
            var schema = new StringBuilder();
            schema.Append($"export interface {type.Name} {{\n");
            foreach (var property in properties)
            {
                schema.Append($"  {property.Name}: {GetSchema(property.PropertyType)};\n");

                // if property is object, get its schema
                if (property.PropertyType.IsClass || property.PropertyType.IsEnum)
                {
                    schema.Append(GetSchema(property.PropertyType));
                }
            }
            schema.Append("}");
            return schema.ToString();
        }
    }

    public class Result<T>
    {
        public bool IsSuccess { get; set; }
        public T? Data { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }
}
