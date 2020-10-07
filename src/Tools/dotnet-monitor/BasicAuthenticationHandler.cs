// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Diagnostics.Monitoring
{
    public class BasicAuthenticationHandler :
        AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public BasicAuthenticationHandler(
           IOptionsMonitor<AuthenticationSchemeOptions> options,
           ILoggerFactory logger,
           UrlEncoder encoder,
           ISystemClock clock)
            : base(options, logger, encoder, clock)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            return Task.FromResult(HandleAuthenticate());
        }

        private AuthenticateResult HandleAuthenticate()
        {
            if (!Request.Headers.TryGetValue("Authorization", out var authHeaderString))
            {
                return AuthenticateResult.NoResult();
            }

            if (!AuthenticationHeaderValue.TryParse(authHeaderString, out var authHeaderValue))
            {
                return AuthenticateResult.NoResult();
            }

            string[] credentials = Encoding.UTF8.GetString(Convert.FromBase64String(authHeaderValue.Parameter)).Split(':');
            if (credentials.Length != 2)
            {
                return AuthenticateResult.NoResult();
            }

            string userName = credentials[0];
            string password = credentials[1];

            // TODO: Get auto generated secret
            if (!string.Equals(password, "monitor", StringComparison.Ordinal))
            {
                return AuthenticateResult.Fail("Invalid user name or password.");
            }

            var claims = new[]
            {
                new Claim(ClaimTypes.Name, userName),
            };
            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            return AuthenticateResult.Success(ticket);
        }

        protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            Response.Headers["WWW-Authenticate"] = "Basic charset=\"UTF-8\"";

            await base.HandleChallengeAsync(properties);
        }
    }
}
