using System;
using System.IO;
using Sandbox.ModAPI;

namespace Bots.Common.Utilities.FileHandlers
{
	public static class Save
	{
		public static void WriteToBinaryFile<T>(string fileName, T data, Type type)
		{
			if (MyAPIGateway.Utilities.FileExistsInWorldStorage(fileName, type))
				MyAPIGateway.Utilities.DeleteFileInWorldStorage(fileName, type);

			using (BinaryWriter binaryWriter = MyAPIGateway.Utilities.WriteBinaryFileInWorldStorage(fileName, type))
			{
				if (binaryWriter == null)
					return;
				byte[] binary = MyAPIGateway.Utilities.SerializeToBinary(data);
				binaryWriter.Write(binary.Length);
				binaryWriter.Write(binary);
			}
		}

		public static void WriteToXmlFile<T>(string fileName, T data, Type type)
		{
			if (MyAPIGateway.Utilities.FileExistsInWorldStorage(fileName, type))
				MyAPIGateway.Utilities.DeleteFileInWorldStorage(fileName, type);

			using (TextWriter textWriter = MyAPIGateway.Utilities.WriteFileInWorldStorage(fileName, type))
			{
				if (textWriter == null)
					return;
				string text = MyAPIGateway.Utilities.SerializeToXML(data);
				textWriter.Write(text.Length);
				textWriter.Write(text);
			}
		}

		public static void WriteToFile<T>(string fileName, T data, Type type)
		{
			if (MyAPIGateway.Utilities.FileExistsInWorldStorage(fileName, type))
				MyAPIGateway.Utilities.DeleteFileInWorldStorage(fileName, type);

			using (TextWriter textWriter = MyAPIGateway.Utilities.WriteFileInWorldStorage(fileName, type))
			{
				if (textWriter == null)
					return;
				textWriter.Write(data);
			}
		}

		public static void WriteToSandbox(Type T)
		{
			
		}
	}
}
