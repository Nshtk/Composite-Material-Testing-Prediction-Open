/*using CommunityToolkit.Mvvm.Messaging;

namespace FCGR.Common.Libraries.General;

public interface IMessageReceiver
{
	public void registerMessages();
}

public interface IMessageSender //TODO async?	//Just a wrapper-indicator that class is using messaging 
{
	public void send<TMessage>(TMessage message) where TMessage : class
	{
		StrongReferenceMessenger.Default.Send<TMessage>(message);
	}
	public void send<TMessage, TToken>(TMessage message, TToken token) where TMessage : class where TToken : IEquatable<TToken>
	{
		StrongReferenceMessenger.Default.Send<TMessage, TToken>(message, token);
	}
}*/

/*public interface IListen
{
}
public interface IListen<T> : IListen
{
	void receive(T message);
}

public static class EventAggregator
{
	public static WeakReferenceMessenger Messenger_weak=new WeakReferenceMessenger();
	public static StrongReferenceMessenger Messenger_strong = new StrongReferenceMessenger();

	static EventAggregator()
	{ }
	private List<IListen> _subscribers = new List<IListen>();

	public void subscribe(IListen subscriber)
	{
		_subscribers.Add(subscriber);
	}

	public void unsubscribe(IListen subscriber)
	{
		_subscribers.Remove(subscriber);
	}

	public void send<T>(T message)
	{
		foreach (var subscriber in _subscribers.OfType<IListen<T>>())
			subscriber.receive(message);
	}
}*/
