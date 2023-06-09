﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
//
// Generated with Bot Builder V4 SDK Template for Visual Studio CoreBot v4.18.1

using AdaptiveCards;
using Microsoft.AspNetCore.Http;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Choices;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Recognizers.Text.DataTypes.TimexExpression;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ToDoBot.Dialogs.Operations;
using ToDoBot.Utilities;

namespace ToDoBot.Dialogs
{
    public class MainDialog : ComponentDialog
    {
        private readonly ToDoLUISRecognizer _luisRecognizer;
        private readonly ILogger _logger;
        private readonly string UserValidationDialogID = "UserValidationDlg";
        private readonly IConfiguration Configuration;
        private CosmosDBClient _cosmosDBClient;

        // Dependency injection uses this constructor to instantiate MainDialog
        public MainDialog(ToDoLUISRecognizer luisRecognizer,ILogger<MainDialog> logger,IConfiguration configuration,CosmosDBClient cosmosDBClient)
            : base(nameof(MainDialog))
        {
            _luisRecognizer = luisRecognizer;
            _logger = logger;
            Configuration = configuration;
            _cosmosDBClient = cosmosDBClient;

            AddDialog(new TextPrompt(nameof(TextPrompt)));
            AddDialog(new TextPrompt(UserValidationDialogID,UserValidation));
            AddDialog(new ChoicePrompt(nameof(ChoicePrompt)));
            AddDialog(new CreateTaskDialog(_cosmosDBClient));
            AddDialog(new ViewTaskDialog(Configuration,_cosmosDBClient));
            AddDialog(new DeleteTaskDialog(_cosmosDBClient));
       

            var waterfallSteps = new WaterfallStep[]
            {
                UserExistsStepAsync,
                UserIDStepAsync,
                IntroStepAsync,
                ActStepAsync,
                FinalStepAsync,
            };

            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), waterfallSteps));

            // The initial child Dialog to run.
            InitialDialogId = nameof(WaterfallDialog);
        }

        private async Task<bool> UserValidation(PromptValidatorContext<string> promptContext, CancellationToken cancellationToken)
        {
            string userId = promptContext.Recognized.Value;
            await promptContext.Context.SendActivityAsync("Please wait, while I Validate your Details...", cancellationToken: cancellationToken);
            if (await _cosmosDBClient.CheckNewUserIdAsync(userId, Configuration["CosmosEndPointURI"], Configuration["CosmosPrimaryKey"], Configuration["CosmosDatabaseId"], Configuration["CosmosContainerId"], Configuration["CosmosPartitionKey"]))
            {
                await promptContext.Context.SendActivityAsync("Your Details are Verified",cancellationToken: cancellationToken);
                User.UserID=userId;
                return true;
            }
            await promptContext.Context.SendActivityAsync("The User Id you entered is not found, Please Correct the ID",cancellationToken:cancellationToken);
            return false; 
        }

        private async Task<DialogTurnResult> UserExistsStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
           if(User.UserID == null)
            {
                List<string> operationList = new List<string> { "Returning User", "New User" };
                // Create card
                var card = new AdaptiveCard(new AdaptiveSchemaVersion(1, 0))
                {
                    // Use LINQ to turn the choices into submit actions
                    Actions = operationList.Select(choice => new AdaptiveSubmitAction
                    {
                        Title = choice,
                        Data = choice, // This will be a string
                    }).ToList<AdaptiveAction>(),
                };
                // Prompt
                return await stepContext.PromptAsync(nameof(ChoicePrompt), new PromptOptions
                {
                    Prompt = (Activity)MessageFactory.Attachment(new Attachment
                    {
                        ContentType = AdaptiveCard.ContentType,
                        // Convert the AdaptiveCard to a Jobject
                        Content = JObject.FromObject(card),
                    }),
                    Choices = ChoiceFactory.ToChoices(operationList),
                    // Don't render the choices outside the card
                    Style = ListStyle.None,
                },
                    cancellationToken);
            }
            else
            {
                return await stepContext.NextAsync(null, cancellationToken);
            }
        }

        private async Task<DialogTurnResult> UserIDStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {

            if(User.UserID == null)
            {
                stepContext.Values["User Type"] = ((FoundChoice)stepContext.Result).Value;
                string userType = (string)stepContext.Values["User Type"];
                string userId = null;

                if ("Returning User".Equals(userType))
                {
                    return await stepContext.PromptAsync(UserValidationDialogID, new PromptOptions
                    {
                        Prompt = MessageFactory.Text("Please Enter your User ID.")
                    }, cancellationToken);
                }
                else
                {
                    do
                    {
                        userId = Repository.RandomString(7);
                    } while (await _cosmosDBClient.CheckNewUserIdAsync(userId, Configuration["CosmosEndPointURI"], Configuration["CosmosPrimaryKey"], Configuration["CosmosDatabaseId"], Configuration["CosmosContainerId"], Configuration["CosmosPartitionKey"]));
                    User.UserID = userId;
                    await stepContext.Context.SendActivityAsync(MessageFactory.Text("Please make a note of your user Id"), cancellationToken);
                    await stepContext.Context.SendActivityAsync(MessageFactory.Text(userId), cancellationToken);
                    return await stepContext.NextAsync(null, cancellationToken);

                }
            }
            else
            {
                return await stepContext.NextAsync(null, cancellationToken);
            }


        }

        // Shows a warning if the requested From or To cities are recognized as entities but they are not in the Airport entity list.
        // In some cases LUIS will recognize the From and To composite entities as a valid cities but the From and To Airport values
        // will be empty if those entity values can't be mapped to a canonical item in the Airport.


        private async Task<DialogTurnResult> IntroStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            await stepContext.Context.SendActivityAsync(MessageFactory.Text("What operation you would like to perform?"), cancellationToken);
            List<string> operationList = new List<string>{"Create Task", "View Task", "Delete Task" };
            // Create card
            var card = new AdaptiveCard(new AdaptiveSchemaVersion(1, 0))
            {
                // Use LINQ to turn the choices into submit actions
                Actions = operationList.Select(choice => new AdaptiveSubmitAction
                {
                    Title = choice,
                    Data = choice, // This will be a string
                }).ToList<AdaptiveAction>(),
            };
        // Prompt
        return await stepContext.PromptAsync(nameof(ChoicePrompt), new PromptOptions
        {
            Prompt = (Activity) MessageFactory.Attachment(new Attachment
            {
                ContentType = AdaptiveCard.ContentType,
                // Convert the AdaptiveCard to a Jobject
                Content = JObject.FromObject(card),
            }),
            Choices = ChoiceFactory.ToChoices(operationList),
            // Don't render the choices outside the card
            Style= ListStyle.None,
        },
            cancellationToken); 
        }

        private async Task<DialogTurnResult> ActStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            stepContext.Values["Operation"] = ((FoundChoice)stepContext.Result).Value;
            string operation = (string)stepContext.Values["Operation"];
            await stepContext.Context.SendActivityAsync(MessageFactory.Text("You have Selected - "+operation), cancellationToken);
            if ("Create Task".Equals(operation))
            {
                return await stepContext.BeginDialogAsync(nameof(CreateTaskDialog), new User(), cancellationToken);
            }
            else if ("View Task".Equals(operation))
            {
                return await stepContext.BeginDialogAsync(nameof(ViewTaskDialog), new User(), cancellationToken);
            }
            else if ("Delete Task".Equals(operation))
            {
                return await stepContext.BeginDialogAsync(nameof(DeleteTaskDialog), new User(), cancellationToken);
            }
            else
            {
                await stepContext.Context.SendActivityAsync(MessageFactory.Text("The selected option not found."),cancellationToken);
                return await stepContext.NextAsync(null, cancellationToken);
            }
        }

        private async Task<DialogTurnResult> FinalStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            
          

            // Restart the main dialog with a different message the second time around
            var promptMessage = "What else can I do for you?";
            return await stepContext.ReplaceDialogAsync(InitialDialogId, promptMessage, cancellationToken);
        }
    }
}
