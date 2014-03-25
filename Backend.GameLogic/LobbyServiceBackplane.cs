﻿namespace Backend.GameLogic
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.ComponentModel.Composition;
    using Microsoft.ServiceBus;
    using Microsoft.ServiceBus.Messaging;

    [Export(typeof(LobbyServiceBackplane))]
    public class LobbyServiceBackplane : IPartImportsSatisfiedNotification
    {
        [Import(typeof(ILobbyServiceSettings))]
        public ILobbyServiceSettings Settings { get; set; }

        public LobbyServiceBackplane() { }

        void IPartImportsSatisfiedNotification.OnImportsSatisfied() 
        {
            this._namespaceManager = NamespaceManager.CreateFromConnectionString(this.Settings.ServiceBusCredentials);
        }

        private NamespaceManager _namespaceManager;

        private const string LobbyServiceTopic = "LobbyServiceTopic";

        public async Task EnsureSetupAsync()    
        {
            if (!await _namespaceManager.TopicExistsAsync(LobbyServiceTopic))
            {
                await _namespaceManager.CreateTopicAsync(LobbyServiceTopic);
            }

            await _namespaceManager.CreateSubscriptionAsync(new SubscriptionDescription(
                topicPath: LobbyServiceTopic, 
                subscriptionName: this.Settings.LobbyServiceInstanceId)
            {
                AutoDeleteOnIdle = TimeSpan.FromMinutes(5)
            });
        }

        public async Task DetachAsync()
        {
            await _namespaceManager.DeleteSubscriptionAsync(
                topicPath: LobbyServiceTopic,
                name: this.Settings.LobbyServiceInstanceId);
        }
    }
}