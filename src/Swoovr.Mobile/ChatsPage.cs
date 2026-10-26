using Swoovr.Client;

namespace Swoovr.Mobile;

/// <summary>Chats (D141): shown in the tab bar, "not available yet" until messaging (slice U, D143) exists.</summary>
public sealed class ChatsPage : ContentPage
{
	public ChatsPage()
	{
		Title = "Chats";
		Content = new ScrollView
		{
			Content = new VerticalStackLayout
			{
				Padding = new Thickness(24, 30), Spacing = 16,
				Children = { Ui.Text("Chats", "PageTitle"), Ui.Text(MemberText.ChatsNotYet, id: "ChatsNotYet") },
			},
		};
	}
}
