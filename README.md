![Muxy Gateway Logo](https://github.com/muxy/gateway-unity/assets/135379/82c89548-7423-4be4-b40b-5728569bcfdf)

> The fastest way to make your game “Twitch friendly”

[Muxy Gateway](https://muxy.io/gateway) is the smartest way to enhance your game
with Twitch viewer interactivity. The Gateway extension lets viewers manipulate
gameplay, see live stats, vote on key game decisions, and more.

## Highlights

- No need to write JS!
- Stable (and fast!) backend.
- Full control over what features your game supports.
- Avoid the Twitch review and approval process.
- Pre-fabs to get started even faster!

## Install the SDK

1. Create a new or load an existing Unity project.
2. [Download Gateway](https://github.com/muxy/gateway-unity/releases)
3. Unzip the package.
4. In the Unity editor, launch the Package Manager from
   `Window > Package Manager` in the main menu.
5. Select `Add package from disk...` from the Add Package dropdown and select
   the `package.json` file from the Gateway folder.

## Using Gateway

There are two flows to choose from when using Gateway:

1. Prefab flow
The prefab flow is recommended if you don't want to deal with a lot of code and want to get things up and running quickly without making your own custom prefabs. All of our prefabs offer lots of settings to tune them just how you want! 

Included prefabs:

-- MuxyGatewayManager
This is the core prefab that all the other prefabs require. It contains the Gateway SDK and handles many things for you such as setting up callbacks, automatic reauthentication, dispatching Unity Events, and more. This is what you will use to interface with Gateway to set specific options, hookup action callbacks, game texts, polling options, and all other settings.

-- MuxyGatewayAuthentication
A Muxy themed authentication for streamers to authenticate/deauthenticate.

-- MuxyGatewayNotifications
Scrolling text notifications of all the actions viewers have taken.

-- MuxyGatewayPolling
Polling widget that shows live results of a poll.

2. Code flow
The code flow is recommended if you prefer a more code oriented workflow and want to make custom prefabs. The SDK code is very easy to work with but you'll have to take care of more things yourself.

## Prefab flow

### Initialization

FIrst add the MuxyGatewayManager prefab into your scene and create another script (we will call it `GameGatewayManager` for the example) and attach it to the MuxyGatewayManager prefab or make a new entity for it, whatever fits best. The `GameGatewayManager` should take MuxyGatewayManager as a field so it can interact with it:

`public MuxyGatewayManager GatewayManager;`

The `GameGatewayManager` script will be used to actually write the code we want for action callbacks and other things. We will add some code to it shortly, but first lets take a look at the settings we should be setting on MuxyGatewayManager.

### Setup MuxyGatewayManager

Game ID: Use `"gateway-testing"` for development, but before going live with an integration, you should
> request a permanent ID for your game by filling out the form
> [here](https://www.muxy.io/gateway).

------------------
Game Metadata: 
- Name: The name of your game (as it will be displayed on the extension side)
- Logo: Your games logo as a Texutre2D
- Theme: Optional frontend extension theme (field can be left blank for default)

------------------
Game Actions:
Game Actions allow viewers to trigger actions in the game by using the action in the extension. 
- ID: The ID of the action
- Category: What effect does the action have on the player or game, available options are (`Neutral, Help, Hinder`)
- State: Dictates if the action is available for purchase or not, available options are (`Unavailable, Available, Hidden`). It's common to set actions to unavailable while paused, switching levels, etc. Making something hidden is useful for actions that may only come up in certain levels or in a bossfight.
- Impact: Determines how much this action will effect the game, available options are (1-5). For example, putting a name over an enemies head doesn't change gameplay much, so it would more than likely be a 1. On the otherhand, instantly killing the player would probably be a 5.
- Name: The action name that will appear in the extension
- Description: The action description that will appear in the extension
- Icon: Icon that will show next to the action. Valid icons can be found [here](http://icon-search.muxy.io/)
- Count: How many times the action can be purchased. Set it to the max allowed number (65535) to represent infinite
- OnGameActionUsed: Add callbacks for when this action is used

------------------
Game Texts:
Setting Game Texts in the editor on the MuxyGatewayManager prefab isn't as useful as all the others generally speaking because Game Texts usually end up being dynamic and not something that is just setup ahead of time. Though it's recommended to setup all the Game Texts elements ahead of time if possible and just update the value text.

- Label: The label for the game text
- Value: The value for the game text
- Icon: Icon that will show next to the text. Valid icons can be found [here](http://icon-search.muxy.io/)

------------------
Poll Configuration:
Poll Configuration can be useful to setup in the editor ahead of time, but your game may have more than one type of poll which will require you to dynamically set the options on the MuxyGatewayManager.

- Prompt: The message text that will be shown to the viewers when the poll runs
- Location: Where it will appear in the extension, available options are (`Default`)
- Mode: Determines how many times a user can vote in a poll. If the mode is set to `Order` it means each viewer can only vote once. If it's set to `Chaos` it means viewers can vote as many times as they'd like
- Options: The poll options the viewers can choose from
- Duration In Seconds: How long the poll will last measured in seconds
- OnPollUpdate: Add callbacks for whenever the poll updates by receiving a vote or finishes by running out of time

------------------
Events:
- OnAuthentication: Callbacks for authentication attempts
- OnAnyGameActionUsed: Callbacks when any game action is used, this will fire even if you have set a specific callback for an action
- OnBitsUsed: Callbacks for when bits/coins are purchased


Now lets take a look at some example code of an action callback and setting some game text. Lets assume we've added the following GameAction to the MuxyGatewayManager:

- ID: "bossSpawn"
- Category: Hinder
- State: Available
- Impact: 4
- Name: "Spawn Mega Boss"
- Description: "Spawn one of the hardest bosses in the game!"
- Icon: "fa-solid:circle-exclamation"
- Count: 3
- OnGameActionUsed: (Set as `OnBossSpawnAction` in `GameGatewayManager`)

and the following GameText:

- Label: "Current Level"
- Value: ""
- Icon: "mdi:format-list-numbered"


```
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameGatewayManager : MonoBehaviour
{
    public MuxyGatewayManager GatewayManager;

    void OnBossSpawnAction(GameActionUsed UsedAction)
    {
        SpawnGameBoss();
    }

    void ChangeLevel(string LevelName)
    {
        GatewayManager.SetGameTextValueAt(LevelName, 0);
    }
}

```

This code assumes that when a level changes, you will call `ChangeLevel`. The `OnBossSpawnAction` will trigger when the action is used. It's that simple!

### Setup Notifications

Settings:
There is multiple settings to give notifications a varied look.
- Clear Time: Time in seconds that all notifications will be cleared
- HelpActionIcon: The help action texture icon that will appear next to the text of the notification 
- NeutralActionIcon: The neutral action texture icon that will appear next to the text of the notification 
- HinderActionIcon: The hinder action texture icon that will appear next to the text of the notification 
- Impact[1-5]Color: The color of the notification text according to that impact level
Messaging:
- Auto Add Action Messages: Notifications will automatically be added when they happen, and no code is required to add them to the notifications. If this setting is turned off, you will need to manually add notifications via code
- Message Template: The message template that will be used when an action is triggered. You can use any of the `GameActionUsed` fields in the template like so `{Username} bought {ActionID} for {Cost}`

### Setup Polling
- Winner Pause Duration: Time in seconds that the vote window will stay open to show the winner

## Code flow

### Initialization

Easily add the Gateway SDK to your game by creating an Entity to control the SDK
lifecycle.

> IMPORTANT: In this example `GameID` is set to the testing value
> `"gateway-testing"`. This GameID is special-cased on Muxy's servers to allow
> limited testing access. Before going live with an integration, you should
> request a permanent ID for your game by filling out the form
> [here](https://www.muxy.io/gateway).

```csharp
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MuxyGateway;

public SDK SDK;
public String GameID = "gateway-testing";
public bool Production;

void Start()
{
    SDK = new(GameID);
    SDK.RunInProduction();
}

public void Update()
{
    SDK.Update();
}
```

### Authenticate Twitch Streamers

From within your game code, you will authenticate a Twitch streamer by passing
along a 6-character PIN that they are shown in the Gateway extension.

```csharp
AuthCB = (Response) =>
    {
        if (Response.HasError)
        {
            // Show an error to the player
            return;
        }

        // Successful auth
    };

SDK.AuthenticateWithPIN(PIN, AuthCB);
```

### Install the Twitch Extension

Begin by installing the
[Gateway Extension](https://www.twitch.tv/ext/i575hs2x9lb3u8hqujtezit03w1740-1.0.0)
on your Twitch Channel. Once installed, click the "Configure" button on the
installation page, this will bring up the interface Twitch Streamers will see
when setting up the extension themselves.

For now, you will only need the "Configuration PIN" to authenticate your testing
channel. **NOTE**: Each PIN will expire if not used within 5 minutes, but a new
PIN may be requested at any time.

<img width="936" alt="Gateway Configuration Page Showing PIN Auth" src="https://github.com/muxy/gateway-unity/assets/135379/813b3ba0-dc37-413e-a325-48659778c501">

## Add Features

### Change the Appearance of the Extension

You can set your game's name, logo and a theme for the extension:

```csharp
Texture2D GameLogo = Resources.Load<Texture2D>("Textures/Gateway/MyGameLogo.png");

GameMetadata Meta = new();
Meta.Name = "My awesome game!";
Meta.Logo = SDK.ConvertTextureToImage(GameLogo);
Meta.Theme = "default";

SDK.SetGameMetadata(Meta);
```

### Show Game Data to Viewers

Send arbitrary text fields to keep viewers informed in realtime:

```csharp
GameText[] Texts =
{
    new GameText
    {
        Label = "Current Level",
        Value = "Menu",
        Icon  = "fa-solid:dungeon"
    },

    new GameText
    {
        Label = "Level Difficulty",
        Value = "Easy",
        Icon  = "fa-solid:bolt-lightning"
    }
}

SDK.SetGameTexts(Texts);
```

### Allow Viewers to Perform In-Game Actions

Set a list of actions that viewers can instantly perform in your game. You get
to define how they appear, how impactful they are to the game and even how many
times an action can be performed:

```csharp
Action[] Actions =
{
    new Action
    {
        ID = "healthpack",
        Name = "Spawn Healthpack",
        Description = "Spawn a healthpack near the player",
        Icon = "fa-solid:heart",
        Category = ActionCategory.Help,
        State = ActionState.Available,
        Impact = 2,
        Count = Action.InfiniteCount,
        Callback = (Action) =>
        {
            GivePlayerHealth();
        }
    },

    new Action
    {
        Name = "Kill Player",
        Description = "Kill the player and make them restart the level!",
        Icon = "fa-solid:skull-crossbones",
        ID = "killplayer",
        Category = ActionCategory.Hinder,
        State = ActionState.Available,
        Impact = 5,
        Count = 1,
        Callback = (Action) =>
        {
            KillThePlayer();
        }
    }
}

SDK.SetActions(Actions);
```

### Poll Viewers to Make Game Decisions

Easily create a poll for Twitch viewers and receive immediate results in your
game:

```csharp
PollConfiguration Config = new();
Config.Mode = PollMode.Order; // PollMode.Chaos would allow everyone to vote as many times as they want
Config.DurationInSeconds = 30;

Config.Prompt = "What should the next level be?";
Config.Options.Add("Ice Cave");
Config.Options.Add("Desert");
Config.Options.Add("Jungle");

Config.OnPollUpdate = (Update) =>
{
    // Receive realtime poll updates or wait until the poll is done
    if (Update.IsFinal)
    {
        if (Update.Winner == 0)      LoadIceCave();
        else if (Update.Winner == 1) LoadDesert();
        else if (Update.Winner == 2) LoadJungle();
    }
}

SDK.StartPoll(Config);
```

## Further Reading

For a complete walkthrough of adding Gateway to your Unity game, and to see all
the cool things you can do with it, visit our full guide
[here](https://docs.muxy.io/docs/unity-gateway-tutorial).
