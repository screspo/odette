﻿#region -- copyright --
//
// Licensed under the EUPL, Version 1.1 or - as soon they will be approved by the
// European Commission - subsequent versions of the EUPL(the "Licence"); You may
// not use this work except in compliance with the Licence.
//
// You may obtain a copy of the Licence at:
// http://ec.europa.eu/idabc/eupl
//
// Unless required by applicable law or agreed to in writing, software distributed
// under the Licence is distributed on an "AS IS" basis, WITHOUT WARRANTIES OR
// CONDITIONS OF ANY KIND, either express or implied. See the Licence for the
// specific language governing permissions and limitations under the Licence.
//
#endregion
using System;
using System.IO;
using System.Threading.Tasks;

namespace TecWare.DE.Odette
{
	#region -- class OdetteNetworkStream ------------------------------------------------

	/// <summary>Network stream implementation for ftp-channel.</summary>
	public sealed class OdetteNetworkStream : IOdetteFtpChannel, IDisposable
	{
		private const byte Version = 1;
		private const byte Flags = 0;
		private const byte StreamTransmissionHeader = (Version << 4) | (Flags & 4);

		private readonly Stream stream;
		private readonly string name;
		private readonly string userData;
		private readonly OdetteCapabilities initialCapabilities;

		#region -- Ctor/Dtor --------------------------------------------------------------

		public OdetteNetworkStream(Stream stream, string name, string userData, OdetteCapabilities initialCapabilities)
		{
			this.stream = stream;
			this.name = name;
			this.userData = userData;
			this.initialCapabilities = initialCapabilities;
		} // ctor

		public void Dispose()
			=> stream.Dispose();

		public Task DisconnectAsync()
			=> Task.Factory.StartNew(stream.Close);

		#endregion

		#region -- Receive ----------------------------------------------------------------

		private async Task ReadAsync(byte[] buffer, int length)
		{
			var ofs = 0;
			do
			{
				var r = await stream.ReadAsync(buffer, ofs, length - ofs);
				ofs += r;
				if (r <= 0)
					throw new OdetteNetworkException("Unexpected end of stream.");
			} while (ofs < length);
		} // func ReadAsync

		public async Task<int> ReceiveAsync(byte[] buffer)
		{
			var header = new byte[4];

			await ReadAsync(header, 4);

			// check signature
			if (header[0] != StreamTransmissionHeader)
				throw new OdetteNetworkException(String.Format("Stream Transmission header is wrong (expected: {0}, found: {1}).", StreamTransmissionHeader, header[0]));

			// read length of buffer
			var length = (header[1] << 16 | header[2] << 8 | header[3]) - 4;
			if (length < 1 || length > buffer.Length)
				throw new OdetteNetworkException(String.Format("Buffer length is invalid (expected: <={0}, found {1})", buffer.Length, length));

			// read data
			await ReadAsync(buffer, length);
			return length;
		} // func ReceiveAsync

		#endregion

		#region -- Send -------------------------------------------------------------------

		public async Task SendAsync(byte[] buffer, int filled)
		{
			var header = new byte[4];
			var tmp = filled + 4;
			header[0] = StreamTransmissionHeader;
			header[1] = unchecked((byte)(tmp >> 16));
			header[2] = unchecked((byte)(tmp >> 8));
			header[3] = unchecked((byte)tmp);
			await stream.WriteAsync(header, 0, 4);
			await stream.WriteAsync(buffer, 0, filled);
		} // proc SendAsync

		#endregion

		public string Name => name;
		public string UserData => userData;
		public OdetteCapabilities InitialCapabilities => initialCapabilities;
	} // class OdetteNetworkStream

	#endregion
}
