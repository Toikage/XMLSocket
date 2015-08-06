using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace AS
{
	public class XMLSocket : IDisposable
	{
		private Socket socket = null;

		public XMLSocket()
		{

		}

		/// <summary>
		/// 切断している時に呼べます。
		/// </summary>
		public void Connect(EndPoint remoteEndPoint, EventHandler onConnected, EventHandler onFailed)
		{
			var arg = new SocketAsyncEventArgs();
			arg.RemoteEndPoint = remoteEndPoint;
			arg.UserToken = onConnected;
			arg.Completed += Arg_ConnectCompleted;
			if(Socket.ConnectAsync(SocketType.Stream, ProtocolType.Tcp, arg) == false)
				if(onFailed != null)
					onFailed(this, EventArgs.Empty);
		}

		/// <summary>
		/// 接続している時に呼べます。
		/// </summary>
		public void Disconnect()
		{
			if(socket != null)
			{
				socket.Shutdown(SocketShutdown.Both);
				socket.Dispose();
				socket = null;
			}
			else
				throw new InvalidOperationException();
		}

		private void Arg_ConnectCompleted(object sender, SocketAsyncEventArgs e)
		{
			if(socket == null)
				socket = e.ConnectSocket;
			else
				throw new InvalidOperationException();
			if(e.UserToken != null)
				(e.UserToken as EventHandler)(this, EventArgs.Empty);
		}

		/// <summary>
		/// XMLの文字列データを送信します。
		/// xml引数が"\0"を含むとXMLSocketの仕様に反します。
		/// 接続している時に呼べます。
		/// </summary>
		/// <param name="xml"></param>
		public void Send(string xml, EventHandler onSent, EventHandler onFailed)
		{
			var arg = new SocketAsyncEventArgs();
			var data = Encoding.ASCII.GetBytes(xml + "\0");
			arg.SetBuffer(data, 0, data.Length);
			arg.UserToken = new Arg_SendUserToken() { onSent = onSent, onFailed = onFailed };
			arg.Completed += Arg_SendCompleted;
			if(socket != null)
			{
				if(socket.SendAsync(arg) == false)
					if(onFailed != null)
						onFailed(this, EventArgs.Empty);
			}
			else
				throw new InvalidOperationException();
		}

		private void Arg_SendCompleted(object sender, SocketAsyncEventArgs e)
		{
			var ut = e.UserToken as Arg_SendUserToken;
			if(ut.onSent != null)
				ut.onSent(this, EventArgs.Empty);
		}

		private class Arg_SendUserToken
		{
			public EventHandler onSent;
			public EventHandler onFailed;
		}

		/// <summary>
		/// 受信をします。
		/// １回呼ぶと、失敗するか切断されるまで監視を行いますので、その間は呼ばないで下さい。
		/// 受信データが揃うとonReceivedが発生します。
		/// 失敗するとonFailedが発生します（未検証）。
		/// </summary>
		public void Receive(EventHandler<XMLSocketOnReceivedArgs> onReceived, EventHandler onFailed)
		{
			Receive_Loop(new Arg_ReceiveUserToken() { onReceived = onReceived, onFailed = onFailed, sb = new StringBuilder() });
		}

		private void Receive_Loop(Arg_ReceiveUserToken ut)
		{
			var arg = new SocketAsyncEventArgs() { UserToken = ut };
			arg.SetBuffer(new byte[256], 0, 256);
			arg.Completed += Arg_ReceiveCompleted;
			if(socket != null)
			{
				if(socket.ReceiveAsync(arg) == false)
					if(ut.onFailed != null)
						ut.onFailed(this, EventArgs.Empty);
			}//else
		}

		private void Arg_ReceiveCompleted(object sender, SocketAsyncEventArgs e)
		{
			var ut = e.UserToken as Arg_ReceiveUserToken;
			for(int i = 0; i < e.BytesTransferred; i++)
			{
				char c = (char)e.Buffer[i];
				if(c != '\0')
				{
					ut.sb.Append(c);
				}
				else
				{
					if(ut.onReceived != null)
						ut.onReceived(this, new XMLSocketOnReceivedArgs() { xml = ut.sb.ToString() });
					ut.sb.Clear();
				}
			}
			Receive_Loop(ut as Arg_ReceiveUserToken);
		}

		private class Arg_ReceiveUserToken
		{
			public EventHandler<XMLSocketOnReceivedArgs> onReceived;
			public EventHandler onFailed;
			public StringBuilder sb;
		}

		public void Dispose()
		{
			if(socket != null)
				((IDisposable)socket).Dispose();
		}
	}

	public class XMLSocketOnReceivedArgs
	{
		public string xml;
	}
}