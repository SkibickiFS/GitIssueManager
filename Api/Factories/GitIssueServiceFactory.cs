using System;
using Application.Enums;
using Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Factories
{
    public class GitIssueServiceFactory : IGitIssueServiceFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public GitIssueServiceFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        public IGitIssueService GetService(ProviderType providerType)
        {
            return _serviceProvider.GetRequiredKeyedService<IGitIssueService>(providerType);
        }
    }
}
