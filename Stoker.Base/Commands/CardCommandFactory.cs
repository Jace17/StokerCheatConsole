using HarmonyLib;
using ShinyShoe.Loading;
using Stoker.Base.Builder;
using Stoker.Base.Extension;
using Stoker.Base.Interfaces;
using System.Reflection;
using TrainworksReloaded.Core;
using TrainworksReloaded.Core.Enum;
using TrainworksReloaded.Core.Interfaces;

namespace Stoker.Base.Commands
{
    public class CardCommandFactory
    {
        private static Lazy<ConsoleLogger> LoggerLazy { get; set; } = new(() => Railend.GetContainer().GetInstance<ConsoleLogger>());

        public static ICommand Create()
        {
            var command = new CommandBuilder("card")
                .WithDescription("Manage cards")
                .WithSubCommand("add")
                    .WithDescription("Add a card to the deck")
                    .WithSimpleNameArg()
                        .WithDescription("The name of the card to add")
                        .WithSuggestions(() =>
                        {
                            Type type = typeof(CheatManager);
                            FieldInfo field = type.GetField("allGameData", BindingFlags.NonPublic | BindingFlags.Static);

                            if (field != null)
                            {
                                AllGameData? allGameData = field.GetValue(null) as AllGameData;
                                if (allGameData != null)
                                {
                                    return [.. allGameData.GetAllCardData().Select(s => s.Cheat_GetNameEnglish())];
                                }
                            }
                            return [];
                        })
                        .WithParser((xs) => xs)
                        .Parent()
                    .SetHandler((args) =>
                    {
                        var arguments = args.Arguments;
                        if (!arguments.ContainsKey("name"))
                            throw new Exception("Missing <name> argument");
                        if (arguments["name"] is not string cardName)
                            throw new Exception("Invalid <name> argument");
                        if (string.IsNullOrEmpty(cardName))
                            throw new Exception("Empty <name> argument");
                        LoggerLazy.Value.Log($"Adding card: {cardName}");
                        CheatManager.Command_AddCard(cardName);
                        return Task.CompletedTask;
                    })
                    .UseHelpMiddleware()
                    .Parent()
                .WithSubCommand("add-random")
                    .WithDescription("Add an amount of random cards to the deck")
                    .WithArgument<int>("amount")
                        .WithDescription("The amount of cards to add")
                        .WithDefaultValue("1")
                        .WithParser((xs) => int.Parse(xs))
                        .Parent()
                    .SetHandler((args) =>
                    {
                        var arguments = args.Arguments;
                        if (!arguments.ContainsKey("amount"))
                            throw new Exception("Missing <amount> argument");
                        if (arguments["amount"] is not int amount)
                            throw new Exception("Invalid <amount> argument");
                        LoggerLazy.Value.Log($"Adding {amount} random cards to the deck");
                        var register = Railend.GetContainer().GetInstance<IRegister<CardData>>();
                        var randomCard = register.GetAllIdentifiers(RegisterIdentifierType.ReadableID);
                        var random = new Random();
                        var saveManager = AccessTools.Field(typeof(CheatManager), "saveManager").GetValue(null) as SaveManager ?? throw new Exception("SaveManager not found");
                        var amountAdded = 0;
                        var attempts = 0;
                        while (amountAdded < amount && attempts < 1000)
                        {
                            attempts++;
                            var index = random.Next(randomCard.Count);
                            var cardData = randomCard[index];
                            register.TryLookupIdentifier(cardData, RegisterIdentifierType.ReadableID, out CardData? cardDataObj, out _);
                            if (cardDataObj == null)
                                throw new Exception("CardData not found");
                            if (cardDataObj.IsUnitAbility())
                                continue;
                            AccessTools.PropertySetter(typeof(CheatManager), "IsBusy").Invoke(null, [true]);
                            LoadingScreen.AddTask(new LoadAdditionalCards(cardDataObj, loadSpawnedCharacters: true, LoadingScreen.DisplayStyle.Spinner, delegate
                            {
                                CardState card2 = saveManager.AddCardToDeck(cardDataObj, null, applyExistingRelicModifiers: true, 0, applyExtraCopiesMutator: false, showAnimation: true);
                                saveManager.DrawSpecificCard(card2);
                                AccessTools.PropertySetter(typeof(CheatManager), "IsBusy").Invoke(null, [false]);
                                LoggerLazy.Value.Log($"Adding <b>{cardDataObj.Cheat_GetNameEnglish()}</b> to hand.");
                            }));
                            amountAdded++;
                        }
                        if (attempts >= 1000)
                            throw new Exception("Failed to add all cards after random attempts.");
                        return Task.CompletedTask;
                    })
                    .UseHelpMiddleware()
                    .Parent()
                .WithSubCommand("remove")
                    .WithDescription("Remove a card from the deck")
                    .WithSimpleNameArg()
                        .WithDescription("The name of the card to remove")
                        .WithSuggestions(() =>
                        {
                            Type type = typeof(CheatManager);
                            FieldInfo field = type.GetField("saveManager", BindingFlags.NonPublic | BindingFlags.Static);

                            if (field != null)
                            {
                                SaveManager? saveManager = field.GetValue(null) as SaveManager;
                                if (saveManager != null)
                                {
                                    return [.. saveManager.GetDeckState().Select(s => s.GetTitleKey().LocalizeEnglish(true, null))];
                                }
                            }
                            return [];
                        })
                        .WithParser((xs) => xs)
                        .Parent()
                    .SetHandler((args) =>
                    {
                        var arguments = args.Arguments;
                        if (!arguments.ContainsKey("name"))
                            throw new Exception("Missing <name> argument");
                        if (arguments["name"] is not string cardName)
                            throw new Exception("Invalid <name> argument");
                        if (string.IsNullOrEmpty(cardName))
                            throw new Exception("Empty <name> argument");
                        LoggerLazy.Value.Log($"Removing card: {cardName}");
                        AccessTools.Method(typeof(CheatManager), "Command_RemoveCard").Invoke(null, [cardName]);
                        return Task.CompletedTask;
                    })
                    .UseHelpMiddleware()
                    .Parent()
                .WithSubCommand("list")
                    .WithDescription("List all cards in the deck")
                    .WithOption<int>("page")
                        .WithDescription("The page number to list")
                        .WithDefaultValue("1")
                        .WithAliases("p")
                        .WithParser((xs) => int.Parse(xs))
                        .Parent()
                    .WithOption<int>("page-size")
                        .WithDescription("The number of cards to list per page")
                        .WithDefaultValue("50")
                        .WithAliases("ps")
                        .WithParser((xs) => int.Parse(xs))
                        .Parent()
                    .SetHandler((args) =>
                    {
                        var options = args.Options;
                        if (!options.ContainsKey("page"))
                            throw new Exception("Missing --page option");
                        if (!options.ContainsKey("page-size"))
                            throw new Exception("Missing --page-size option");
                        if (options["page"] is not int page)
                            throw new Exception("Invalid --page option");
                        if (options["page-size"] is not int pageSize)
                            throw new Exception("Invalid --page-size option");


                        Type type = typeof(CheatManager);
                        FieldInfo field = type.GetField("allGameData", BindingFlags.NonPublic | BindingFlags.Static);

                        if (field != null)
                        {
                            AllGameData? allGameData = field.GetValue(null) as AllGameData;
                            if (allGameData != null)
                            {
                                List<CardData> cards = allGameData.GetAllCardData().ToList();
                                cards.FindAll(c => c.IsUnitAbility()).ForEach(c => cards.Remove(c)); // Remove unit abilities
                                cards.Sort((x, y) => string.Compare(x.Cheat_GetNameEnglish(), y.Cheat_GetNameEnglish(), StringComparison.OrdinalIgnoreCase));
                                var startIndex = (page - 1) * pageSize;
                                var endIndex = startIndex + pageSize;
                                var pageCards = cards.Skip(startIndex).Take(pageSize);
                                LoggerLazy.Value.Log("Cards:");
                                foreach (var card in pageCards)
                                {
                                    LoggerLazy.Value.Log($"{card.Cheat_GetNameEnglish()}");
                                }
                            }
                        }

                        return Task.CompletedTask;
                    })
                    .UseHelpMiddleware()
                    .Parent()
                .UseHelpMiddleware()
                .Build();
            return command;
        }
    }
}
