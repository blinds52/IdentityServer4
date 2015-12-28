﻿using IdentityServer4.Core.Extensions;
using IdentityServer4.Core.Hosting;
using IdentityServer4.Core.Models;
using IdentityServer4.Core.Results;
using IdentityServer4.Core.Services;
using IdentityServer4.Core.Validation;
using IdentityServer4.Core.ViewModels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.WebEncoders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace IdentityServer4.Core.ResponseHandling
{
    class AuthorizationResultGenerator : IAuthorizationResultGenerator
    {
        private readonly ILogger<AuthorizationResultGenerator> _logger;
        private readonly IdentityServerContext _context;
        private readonly ILocalizationService _localizationService;
        private readonly IHtmlEncoder _encoder;
        private readonly ClientListCookie _clientListCookie;

        public AuthorizationResultGenerator(
            ILogger<AuthorizationResultGenerator> logger,
            IdentityServerContext context,
            ILocalizationService localizationService,
            IHtmlEncoder encoder,
            ClientListCookie clientListCookie)
        {
            _logger = logger;
            _context = context;
            _localizationService = localizationService;
            _encoder = encoder;
            _clientListCookie = clientListCookie;
        }

        public Task<IResult> CreateConsentResultAsync()
        {
            return Task.FromResult<IResult>(new ConsentPageResult());

            //string loginWithDifferentAccountUrl = null;
            //if (validatedRequest.HasIdpAcrValue() == false)
            //{
            //    loginWithDifferentAccountUrl = Url.Route(Constants.RouteNames.Oidc.SwitchUser, null)
            //        .AddQueryString(requestParameters.ToQueryString());
            //}

            //var env = Request.GetOwinEnvironment();
            //var consentModel = new ConsentViewModel
            //{
            //    RequestId = env.GetRequestId(),
            //    SiteName = _options.SiteName,
            //    SiteUrl = env.GetIdentityServerBaseUrl(),
            //    ErrorMessage = errorMessage,
            //    CurrentUser = env.GetCurrentUserDisplayName(),
            //    LogoutUrl = env.GetIdentityServerLogoutUrl(),
            //    ClientName = validatedRequest.Client.ClientName,
            //    ClientUrl = validatedRequest.Client.ClientUri,
            //    ClientLogoUrl = validatedRequest.Client.LogoUri,
            //    IdentityScopes = validatedRequest.GetIdentityScopes(this._localizationService),
            //    ResourceScopes = validatedRequest.GetResourceScopes(this._localizationService),
            //    AllowRememberConsent = validatedRequest.Client.AllowRememberConsent,
            //    RememberConsent = consent == null || consent.RememberConsent,
            //    LoginWithDifferentAccountUrl = loginWithDifferentAccountUrl,
            //    ConsentUrl = Url.Route(Constants.RouteNames.Oidc.Consent, null).AddQueryString(requestParameters.ToQueryString()),
            //    AntiForgery = _antiForgeryToken.GetAntiForgeryToken()
            //};

            //return new ConsentActionResult(_viewService, consentModel, validatedRequest);
        }

        public Task<IResult> CreateErrorResultAsync(ErrorTypes errorType, string error, ValidatedAuthorizeRequest request)
        {
            var msg = _localizationService.GetMessage(error);
            if (msg.IsMissing())
            {
                msg = error;
            }

            var errorModel = new ErrorViewModel
            {
                RequestId = _context.GetRequestId(),
                ErrorCode = error,
                ErrorMessage = msg
            };

            // if this is a client error, we need to build up the 
            // response back to the client, and provide it in the 
            // error view model so the UI can build the link/form
            if (errorType == ErrorTypes.Client)
            {
                errorModel.ReturnInfo = new ClientReturnInfo
                {
                    ClientId = request.ClientId,
                    ClientName = request.Client.ClientName,
                };

                var response = new AuthorizeResponse
                {
                    Request = request,
                    IsError = true,
                    Error = error,
                    State = request.State,
                    RedirectUri = request.RedirectUri
                };

                if (request.ResponseMode == Constants.ResponseModes.Query ||
                         request.ResponseMode == Constants.ResponseModes.Fragment)
                {
                    errorModel.ReturnInfo.Uri = request.RedirectUri = AuthorizeRedirectResult.BuildUri(response);
                }
                else if (request.ResponseMode == Constants.ResponseModes.FormPost)
                {
                    errorModel.ReturnInfo.Uri = request.RedirectUri;
                    errorModel.ReturnInfo.PostBody = AuthorizeFormPostResult.BuildFormBody(response, _encoder);
                }
                else
                {
                    _logger.LogError("Unsupported response mode.");
                    throw new InvalidOperationException("Unsupported response mode");
                }
            }

            return Task.FromResult<IResult>(new ErrorPageResult(errorModel));
        }

        public Task<IResult> CreateLoginResultAsync(SignInMessage message)
        {
            return Task.FromResult<IResult>(new LoginPageResult(message));
        }

        public Task<IResult> CreateAuthorizeResultAsync(AuthorizeResponse response)
        {
            var request = response.Request;

            if (request.ResponseMode == Constants.ResponseModes.Query ||
                request.ResponseMode == Constants.ResponseModes.Fragment)
            {
                _logger.LogDebug("Adding client {0} to client list cookie for subject {1}", request.ClientId, request.Subject.GetSubjectId());
                _clientListCookie.AddClient(request.ClientId);

                return Task.FromResult<IResult>(new AuthorizeRedirectResult(response));
            }

            if (request.ResponseMode == Constants.ResponseModes.FormPost)
            {
                _logger.LogDebug("Adding client {0} to client list cookie for subject {1}", request.ClientId, request.Subject.GetSubjectId());
                _clientListCookie.AddClient(request.ClientId);

                return Task.FromResult<IResult>(new AuthorizeFormPostResult(response, _encoder));
            }

            _logger.LogError("Unsupported response mode.");
            throw new InvalidOperationException("Unsupported response mode");
        }
    }
}