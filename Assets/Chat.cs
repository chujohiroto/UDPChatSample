using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using UnityEngine;
using UnityEngine.UI;
using System.Net;
using LumiSoft.Net.STUN.Client;
using System.Text;
using System;
using System.Threading.Tasks;

public class Chat : MonoBehaviour
{
	public int BindPort = 10000;
	public bool isStun;

	private IPEndPoint MyIPEndPoint;

	[SerializeField]
	private Text ChatLog;
	[SerializeField]   
	private Button ButtonEnterChat;
	[SerializeField]
	private InputField InputChat;
	[SerializeField]
	private InputField InputAddress;
	[SerializeField]
    private InputField InputPort;
	[SerializeField]
	private Button ButtonEnterAddress;

	[SerializeField]
	private Text MyEndPoint;

	private UdpClient UdpClient;

	private IPEndPoint ConnectIPEndPoint = new IPEndPoint(0,0);

	private bool isConnect;

	private async void Start()
	{
		UdpClient = new UdpClient(BindPort);

		if (!isStun)
		{
			//Private IP を取る手段　もうちょいいい方法求む
			using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.IPv6HopByHopOptions))
			{
				socket.Connect("8.8.8.8", 65530);
				IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
				MyIPEndPoint = endPoint;
				MyIPEndPoint.Port = BindPort;
			}
		}

        if (isStun)
        {
            STUN(UdpClient);
        }


		MyEndPoint.text = MyIPEndPoint.ToString();
        
		ButtonEnterAddress.onClick.AddListener(Connect);
		ButtonEnterChat.onClick.AddListener(Send);
		await ReceiveLoop();
	}

	private void STUN(UdpClient client)
	{
		// Query STUN server
        //GoogleのSTUNサーバーから自分のグローバルIPアドレスとNAPTで変換されたポートを取得する
		STUN_Result result = STUN_Client.Query("stun.l.google.com", 19302, client.Client);
        //もし自分のネット環境がUDPをブロックする場合
		if (result.NetType == STUN_NetType.UdpBlocked)
		{
			Debug.LogError("UDP blocked");
		}
		else
		{
			//取得に成功した場合
			Debug.Log(result.NetType.ToString());
			IPEndPoint publicEP = result.PublicEndPoint;
			Debug.Log("Global IP:" + publicEP.Address);
			Debug.Log("NAT Port:" + publicEP.Port);
			MyIPEndPoint = publicEP;
		}
	}

	private async Task ReceiveLoop()
	{
		for (; ;)
		{
			var result = await UdpClient.ReceiveAsync();
			//接続先以外のパケットが流れてきたらブロックする
			if (result.RemoteEndPoint.Address != ConnectIPEndPoint.Address)
			{
				//接続要求であればそのまま許可する
				if (Encoding.UTF8.GetString(result.Buffer) == "e:Connect")
				{
					ConnectIPEndPoint = result.RemoteEndPoint;
					ChatLog.text += "\n Connect Request";
					var bytesData = Encoding.UTF8.GetBytes("e:Accept");
					UdpClient.Send(bytesData, bytesData.Length, ConnectIPEndPoint);
					isConnect = true;
				}
				// 接続許可であればそのまま通信しはじめる
				else if (Encoding.UTF8.GetString(result.Buffer) == "e:Accept")
				{
					ConnectIPEndPoint = result.RemoteEndPoint;
					ChatLog.text += "\n Connect Start";
					isConnect = true;
				}
				else
				{
					Debug.Log("Block");
				}
			}
			else
			{
				Debug.Log(result.RemoteEndPoint.ToString());
				ReceiveCallback(result.Buffer);
			}
		}
	}
    
	private async void Connect()
	{
		while (!isConnect)
		{
			Debug.Log("Connecting...");
            var bytesData = Encoding.UTF8.GetBytes("e:Connect");
			UdpClient.Send(bytesData, bytesData.Length, new IPEndPoint(IPAddress.Parse(InputAddress.text), int.Parse(InputPort.text)));
			await Task.Delay(1000);
		}
	}

	public async void Send()
	{
		var bytesData = Encoding.UTF8.GetBytes(InputChat.text);
		await UdpClient.SendAsync(bytesData, bytesData.Length, ConnectIPEndPoint);
		Debug.Log("Send:" + InputChat.text);
		ChatLog.text += "\n My:" + InputChat.text;
		InputChat.text = "";
	}

	public void ReceiveCallback(byte[] receiveBytes)
    {
		string receiveString = Encoding.UTF8.GetString(receiveBytes);
		if (receiveString == null || receiveString == "")
        {
            return;
        }
		if(receiveString == "e:Accept"){
			Debug.Log("Accept: " + receiveString);
			ChatLog.text += "\n Connect Accept";
		}
		Debug.Log("Received: " + receiveString);
        
    	ChatLog.text += "\n Other:" + receiveString;
	}
}