# Account Linking

# Introduction

Stash connects players’ game accounts with your web shop via one-time **deep linking**. This feature allows players to connect their gaming accounts to the web shop without the need to manually copy and paste account IDs or follow complicated steps. 

Players simply link their game to web shop with a single tap of a button or by scanning a QR code on another device. Once their game account is linked once, they can shop anytime they want.

![RPReplay_Final1709380141-ezgif.com-optimize.gif](Account%20Linking%20078afb7dccc1476fb1c3b116dd88f20c/RPReplay_Final1709380141-ezgif.com-optimize.gif)

As you can see in the video, the player taps on an account linking button in the web shop and the game binary is launched. The user is then prompted to approve or decline the account linking request inside the game. The whole flow is highly customizable with support for different authentication providers.

---

## Stash’s URL scheme

Lets look at the URL Scheme that the Stash web store uses to launch the game and perform the linking:

`stashggsample://link?challenge=QBHb5sIdj5RpEJTYZU2_mxUDLml1PRbd0Io5I2g8oVg**=**` 

The most important parts of the link are the **custom protocol** and the **code challenge**. The custom protocol is a unique identifier agreed upon between Stash and the developer in advance. The content of the entire link is then passed over to the game binary. We leverage the code challenge to complete the account linking process.

---

# Integration Path

## Step 1 : Import Stash Package

You can either call the Stash API directly or import our Unity package to simplify the integration. Our package is extremely lightweight with no external dependencies and wraps all of our API calls.

1. Download the latest release of the [Stash Client for Unity](https://github.com/stashgg/stash-unity).
2. Import the `.unitypackage` file into your Unity game using the [local asset package import(opens in a new tab)](https://docs.unity3d.com/Manual/AssetPackagesImport.html) process.
3. Optionally select the `Scenes` folder to import demo scene.

---

## Step 2 : Set up deep linking in Unity

Before you can process account linking in the game client as demonstrated above, you need to configure your Unity project to handle URL schemes. The following steps are applicable to iOS builds:

1. Go to **Edit** > **Project Settings** > **Player** > **Other Settings** > **Configuration**.
2. Expand **Supported URL schemes** to set the following properties:
    - **Size** property to `1`.
    - **Element 0** property to the URL scheme to use with your application. For example, use `stashggsample`to open your game when the device processes a link. *(Your Stash URL scheme will be agreed in advance, for example* `tacticusshop`*)*
    
    ![Untitled](Account%20Linking%20078afb7dccc1476fb1c3b116dd88f20c/Untitled.png)
    
    ```csharp
    	 public void onDeepLinkActivated(string url)
    	 {
    				//Extract the challenge parameter from the link.
            var challenge = url.Split("/link?challenge=")[1];
            if (!string.IsNullOrEmpty(challenge))
            {
    						try
    				    {
    							//Use Stash package to link accounts.
    							await StashClient.Instance.LinkWithApple(challenge,playerId,identityToken);
    							Debug.Log("Stash link was successful.");
    						}
    				    catch (StashLinkException ex)
    				    {
    			        Debug.LogException(ex);
    				    }
            }
        }
    ```
    

<aside>
💡 The deep links scheme setup in Unity is straightforward, but platform specific. Follow the guides for [Android](https://docs.unity3d.com/Manual/deep-linking-android.html) or [Windows](https://docs.unity3d.com/Manual/deep-linking-universal-windows-platform.html).

</aside>

1. With URL schemes properly setup, the game can now be opened from the Stash web store and the account linking challenge can be passed over for the game client to complete the account linking.

---

## Step 3 : Extracting the code challenge

Once your project is configured to handle the deep linking, it’s time to **extract the code challenge**. To do that, we use the Unity’s `Application.deepLinkActivated` event. Please, follow the [official Unity guide](https://docs.unity3d.com/Manual/deep-linking.html) to learn more.

<aside>
💡 This is good spot to adjust your account linking flow ! We recommend to prompt the user to approve or decline the account linking.

</aside>

**Integration Example:** Event handler **onDeepLinkActivated** is invoked every time the game is launched or resumed via the Stash’s deep link. 

```csharp
	 
Application.deepLinkActivated += onDeepLinkActivated;
.
.
public void onDeepLinkActivated(string url)
{
		//Extract the challenge parameter from the link.
    var challenge = url.Split("/link?challenge=")[1];
    if (!string.IsNullOrEmpty(challenge))
    {
			//Work with code challenge in the next step....
    }
}
```

---

## Step 4 : Linking account

Now, let’s call **StashClient** link function ****to complete the linking process. Stash library contains separate linking methods for following authentication providers out of the box:

- Unity Authentication / Unity Player Accounts
- Apple ID
- Apple Game Center
- Google
- Google Play Games

<aside>
💡 We also support other/custom authentication providers. Contact us to discuss the integration path.

</aside>

Pass the code challenge and user’s token to appropriate linking function, and that’s it, you can now inform users to navigate back to the web store, as the linking process is completed. 

Wrap the link request in the try/catch block to handle issues during the linking process.

---