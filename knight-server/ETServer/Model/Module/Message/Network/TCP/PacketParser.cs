﻿using System;

namespace ETModel
{
	internal enum ParserState
	{
		PacketSize,
		PacketBody
	}
	
	public class Packet
	{
		public const int MinSize = 3;
		public const int MaxSize = 60000;
		public const int FlagIndex = 0;
		public const int OpcodeIndex = 1;
		public const int Index = 3;

		/// <summary>
		/// 只读，不允许修改
		/// </summary>
		public byte[] Bytes { get; }
		public ushort Length { get; set; }
		public byte Flag { get; set; }
		public ushort Opcode { get; set; }

		public Packet(int length)
		{
			this.Length = 0;
			this.Bytes = new byte[length];
		}

		public Packet(byte[] bytes)
		{
			this.Bytes = bytes;
			this.Length = (ushort)bytes.Length;
		}
	}

	internal class PacketParser
	{
		private readonly CircularBuffer buffer;
		private ushort packetSize;
		private ParserState state;
		private readonly Packet packet = new Packet(ushort.MaxValue);
		private bool isOK;

		public PacketParser(CircularBuffer buffer)
		{
			this.buffer = buffer;
		}

		public bool Parse()
		{
			if (this.isOK)
			{
				return true;
			}

			bool finish = false;
			while (!finish)
			{
				switch (this.state)
				{
					case ParserState.PacketSize:
						if (this.buffer.Length < 2)
						{
							finish = true;
						}
						else
						{
							this.buffer.Read(this.packet.Bytes, 0, 2);
							this.packetSize = BitConverter.ToUInt16(this.packet.Bytes, 0);
							if (packetSize < Packet.MinSize || packetSize > Packet.MaxSize)
							{
								throw new Exception($"packet size error: {this.packetSize}");
							}
							this.state = ParserState.PacketBody;
						}
						break;
					case ParserState.PacketBody:
						if (this.buffer.Length < this.packetSize)
						{
							finish = true;
						}
						else
						{
							this.buffer.Read(this.packet.Bytes, 0, this.packetSize);
							this.packet.Length = this.packetSize;
							this.packet.Flag = this.packet.Bytes[0];
							this.packet.Opcode = BitConverter.ToUInt16(this.packet.Bytes, Packet.OpcodeIndex);
							this.isOK = true;
							this.state = ParserState.PacketSize;
							finish = true;
						}
						break;
				}
			}
			return this.isOK;
		}

		public Packet GetPacket()
		{
			this.isOK = false;
			return this.packet;
		}
	}
}