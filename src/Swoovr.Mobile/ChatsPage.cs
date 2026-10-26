using Swoovr.Client;

namespace Swoovr.Mobile;

/// <summary>Chats (D141, D143), as on the web: the member's open conversations, most recent first; tap one to open it.</summary>
public sealed class ChatsPage : ContentPage
{
	private readonly VerticalStackLayout body = new() { Padding = new Thickness(24, 30), Spacing = 16 };

	public ChatsPage()
	{
		Title = "Chats";
		Content = new ScrollView { Content = body };
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		body.Children.Clear();
		body.Children.Add(Ui.Text("Chats", "PageTitle"));
		if (!await Ui.SignedInAsync()) { body.Children.Add(Ui.Text("Sign in on the Me tab to see your chats.", id: "ChatsSignedOut")); return; }
		var result = await Ui.Members!.ConversationsAsync();
		if (result.Value is not { } conversations) { body.Children.Add(Ui.Barred(result.Code, "ChatsBarred") ?? Ui.Text("We couldn't load your chats.", id: "ChatsRefused")); return; }
		if (conversations.Count == 0) body.Children.Add(Ui.Text("No chats yet. Start one from a connection's profile.", id: "ChatsEmpty"));
		foreach (var c in conversations)
			body.Children.Add(Ui.Card(
				new HorizontalStackLayout { Spacing = 8, Children = { Ui.Text(c.Person.DisplayName ?? "Someone", "TileName", $"Chat_{c.Id}"), Ui.BadgeRow(c.Person.Badges, $"ChatBadges_{c.Id}") } },
				Ui.Text(c.LastMessageAt is { } at ? at.ToLocalTime().ToString("d MMM, HH:mm") : "No messages yet", "FooterClaims"),
				Ui.Button("Open", $"ChatOpen_{c.Id}", () => Ui.PushAsync(new ConversationPage(c.Id)), primary: false)));
	}
}
