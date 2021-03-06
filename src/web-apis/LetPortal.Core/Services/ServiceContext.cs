﻿using System;
using System.Net.Http;
using LetPortal.Core.Logger;
using LetPortal.Core.Logger.Models;
using LetPortal.Core.Monitors;
using LetPortal.Core.Monitors.Models;
using LetPortal.Core.Services.Models;
using Microsoft.Extensions.Options;

namespace LetPortal.Core.Services
{
    public class ServiceContext : IServiceContext
    {
        private static string serviceId;

        private static Lazy<int> numberOfRequests = new Lazy<int>(() => 0);

        private readonly IOptionsMonitor<ServiceOptions> _serviceOptions;

        private readonly IOptionsMonitor<LoggerOptions> _loggerOptions;

        private readonly IOptionsMonitor<MonitorOptions> _monitorOptions;

        private readonly HttpClient _httpClient;

        public ServiceContext(
            IOptionsMonitor<ServiceOptions> serviceOptions,
            IOptionsMonitor<MonitorOptions> monitorOptions,
            IOptionsMonitor<LoggerOptions> loggerOptions,
            HttpClient httpClient)
        {
            _serviceOptions = serviceOptions;
            _monitorOptions = monitorOptions;
            _loggerOptions = loggerOptions;
            _httpClient = httpClient;
        }

        public void Start(Action postAction)
        {
            try
            {
                var task = _httpClient.PostAsJsonAsync(_serviceOptions.CurrentValue.ServiceManagementEndpoint + "/api/services/register", new RegisterServiceModel
                {
                    ServiceName = _serviceOptions.CurrentValue.Name,
                    Version = _serviceOptions.CurrentValue.Version,
                    ServiceHardwareInfo = new ServiceHardwareInfo
                    {
                        Directory = Environment.CurrentDirectory,
                        MachineName = Environment.MachineName,
                        Os = Environment.OSVersion.VersionString,
                        ProcessorCores = Environment.ProcessorCount
                    },
                    LoggerNotifyEnable = _loggerOptions.CurrentValue.NotifyOptions.Enable
                });
                var httpResponseMessage = task.Result;
                httpResponseMessage.EnsureSuccessStatusCode();
                serviceId = httpResponseMessage.Content.ReadAsStringAsync().Result;
                if (postAction != null)
                {
                    postAction.Invoke();
                }
            }
            catch (Exception ex)
            {
                throw new Exception("There are some erros when trying to connect Service Management, please check stack trace.", ex);
            }
        }

        public void Stop(Action postAction)
        {
            try
            {
                var task = _httpClient.GetAsync(_serviceOptions.CurrentValue.ServiceManagementEndpoint + "/api/services/shutdown/" + serviceId);
                var httpResponseMessage = task.Result;
                httpResponseMessage.EnsureSuccessStatusCode();
                serviceId = httpResponseMessage.Content.ReadAsStringAsync().Result;

                if (postAction != null)
                {
                    postAction.Invoke();
                }
            }
            catch (Exception ex)
            {
                throw new Exception("There are some erros when trying to notify stop behavior in Service Management, please check stack trace.", ex);
            }
        }

        public void Run(Action postAction)
        {
            try
            {
                var task = _httpClient.PutAsync(_serviceOptions.CurrentValue.ServiceManagementEndpoint + "/api/services/" + serviceId, new StringContent(string.Empty));
                var httpResponseMessage = task.Result;
                httpResponseMessage.EnsureSuccessStatusCode();

                if (postAction != null)
                {
                    postAction.Invoke();
                }
            }
            catch (Exception ex)
            {
                throw new Exception("There are some erros when trying to notify stop behavior in Service Management, please check stack trace.", ex);
            }
        }

        public void PushLog(PushLogModel pushLogModel)
        {
            if (_loggerOptions.CurrentValue.NotifyOptions.Enable)
            {
                pushLogModel.RegisteredServiceId = serviceId;
                pushLogModel.ServiceName = _serviceOptions.CurrentValue.Name;
                try
                {
                    var task = _httpClient.PostAsJsonAsync(_serviceOptions.CurrentValue.ServiceManagementEndpoint + "/api/logs", pushLogModel);
                    var httpResponseMessage = task.Result;
                    httpResponseMessage.EnsureSuccessStatusCode();
                }
                catch (Exception ex)
                {
                    throw new Exception("There are some erros when trying to notify log behavior in Service Management, please check stack trace.", ex);
                }
            }
        }

        public void PushHealthCheck(PushHealthCheckModel pushHealthCheckModel)
        {
            if (_monitorOptions.CurrentValue.NotifyOptions.Enable)
            {
                try
                {
                    pushHealthCheckModel.ServiceId = serviceId;
                    var task = _httpClient.PostAsJsonAsync(
                        (!string.IsNullOrEmpty(_monitorOptions.CurrentValue.NotifyOptions.HealthcheckEndpoint)
                            ? _monitorOptions.CurrentValue.NotifyOptions.HealthcheckEndpoint : (_serviceOptions.CurrentValue.ServiceManagementEndpoint + "/api/monitors")), pushHealthCheckModel);
                    var httpResponseMessage = task.Result;
                    httpResponseMessage.EnsureSuccessStatusCode();
                }
                catch (Exception ex)
                {
                    throw new Exception("There are some erros when trying to notify monitor behavior in Service Management, please check stack trace.", ex);
                }
            }
        }
    }
}
