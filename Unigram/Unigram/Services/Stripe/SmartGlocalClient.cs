﻿using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Json;

namespace Unigram.Services.Stripe
{
    public class SmartGlocalClient : IDisposable
    {
        private readonly string _publicToken;
        private HttpClient _client;

        public SmartGlocalClient(string publicToken)
        {
            _publicToken = publicToken;
            _client = new HttpClient();
        }

        public async Task<string> CreateTokenAsync(Card card, bool test)
        {
            if (card == null)
            {
                throw new ArgumentNullException(nameof(card));
            }

            if (_client != null)
            {
                try
                {
                    var parameters = new JsonObject
                    {
                        {
                            "card", new JsonObject
                            {
                                { "number", JsonValue.CreateStringValue(card.Number) },
                                { "expiration_month", JsonValue.CreateStringValue(card.ExpiryMonth.ToString()) },
                                { "expiration_year", JsonValue.CreateStringValue(card.ExpiryYear.ToString()) },
                                { "security_code", JsonValue.CreateStringValue(card.CVC) }
                            }
                        }
                    };

                    var body = parameters.ToString();
                    var url = test ? "https://tgb-playground.smart-glocal.com/cds/v1/tokenize/card" : "https://tgb.smart-glocal.com/cds/v1/tokenize/card";

                    var request = new HttpRequestMessage(HttpMethod.Post, url);
                    var requestContent = new StringContent(body, Encoding.UTF8, "application/json");

                    request.Headers.TryAddWithoutValidation("X-PUBLIC-TOKEN", _publicToken);
                    request.Content = requestContent;

                    var response = await _client.SendAsync(request);
                    var content = await response.Content.ReadAsStringAsync();
                    var json = JsonObject.Parse(content);

                    var resultData = json.GetNamedObject("data", null);
                    if (resultData == null)
                    {
                        return null;
                    }

                    var token = resultData.GetNamedString("token", string.Empty);
                    if (token == null)
                    {
                        return null;
                    }

                    return token;
                }
                catch
                {

                }
            }

            return null;
        }

        public void Dispose()
        {
            if (_client != null)
            {
                _client.Dispose();
                _client = null;
            }
        }
    }
}
