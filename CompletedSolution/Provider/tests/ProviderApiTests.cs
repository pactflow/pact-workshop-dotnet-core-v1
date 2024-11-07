using System;
using System.IO;
using System.Collections.Generic;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using PactNet.Infrastructure.Outputters;
using PactNet.Output.Xunit;
using PactNet.Verifier;
using Xunit;
using Xunit.Abstractions;
using Provider;

namespace tests
{
    public class ProviderApiTests : IDisposable
    {
        private string _providerUri { get; }
        private string _pactServiceUri { get; }
        private IWebHost _providerStateHost { get; }
        private IWebHost _sut { get; }
        private ITestOutputHelper _outputHelper { get; }

        public ProviderApiTests(ITestOutputHelper output)
        {
            _outputHelper = output;
            _providerUri = "http://localhost:9000";
            _pactServiceUri = "http://localhost:9001";

            _providerStateHost = WebHost.CreateDefaultBuilder()
                .UseUrls(_pactServiceUri)
                .UseStartup<TestStartup>()
                .Build();
            _providerStateHost.Start();

            _sut = WebHost.CreateDefaultBuilder()
            .UseUrls(_providerUri)
            .UseStartup<Startup>()
            .Build();

            _sut.Start();
        }

        [Fact]
        public void EnsureProviderApiHonoursPactWithConsumer()
        {
            // Arrange
            var config = new PactVerifierConfig
            {

                // NOTE: We default to using a ConsoleOutput,
                // however xUnit 2 does not capture the console output,
                // so a custom outputter is required.
                Outputters = new List<IOutput>
                                {
                                    new XunitOutput(_outputHelper)
                                },

                // Output verbose verification logs to the test output
                LogLevel = PactNet.PactLogLevel.Debug
            };

            //Act / Assert
            string version = "2.4.1-f3842db9e603d7"; // hard-coded for demonstration
            string branch = "master"; // hard-coded for demonstration
            bool PublishVerificationResults = true; // hard-coded for demonstration

            IPactVerifier pactVerifier = new PactVerifier("Provider", config);
                pactVerifier.WithHttpEndpoint(new Uri(_providerUri))
                .WithPactBrokerSource(new Uri(Environment.GetEnvironmentVariable("PACT_BROKER_BASE_URL")), options =>
                {
                    options.ConsumerVersionSelectors(
                                new ConsumerVersionSelector { DeployedOrReleased = true },
                                new ConsumerVersionSelector { MainBranch = true },
                                new ConsumerVersionSelector { MatchingBranch = true }
                            )
                            .ProviderBranch(branch)
                            .PublishResults(PublishVerificationResults, version, results =>
                            {
                                results.ProviderBranch(branch);
                            })
                            .EnablePending()
                            .IncludeWipPactsSince(new DateTime(2022, 1, 1));
                        options.TokenAuthentication(Environment.GetEnvironmentVariable("PACT_BROKER_TOKEN"));
                })
                .WithProviderStateUrl(new Uri($"{_pactServiceUri}/provider-states"))
                .Verify();
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    
                    _sut.StopAsync().GetAwaiter().GetResult();
                    _sut.Dispose();
                    _providerStateHost.StopAsync().GetAwaiter().GetResult();
                    _providerStateHost.Dispose();
                }

                disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }
        #endregion
    }
}
