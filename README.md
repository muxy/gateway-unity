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

## Set up Gateway

### Initialize the SDK

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
