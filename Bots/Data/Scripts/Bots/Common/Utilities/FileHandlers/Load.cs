using System;
using System.IO;
using Sandbox.ModAPI;

namespace Bots.Common.Utilities.FileHandlers
{
	internal static class Load
	{
		public static T ReadFromBinaryFile<T>(string fileName, Type type)
		{
			if (!MyAPIGateway.Utilities.FileExistsInWorldStorage(fileName, type))
				return default(T);

			using (BinaryReader binaryReader = MyAPIGateway.Utilities.ReadBinaryFileInWorldStorage(fileName, type))
			{
				return MyAPIGateway.Utilities.SerializeFromBinary<T>(binaryReader.ReadBytes(binaryReader.ReadInt32()));
			}
		}

		public static T ReadFromXmlFile<T>(string fileName, Type type)
		{	
			if (!MyAPIGateway.Utilities.FileExistsInWorldStorage(fileName, type))
				return default(T);

			using (TextReader textReader = MyAPIGateway.Utilities.ReadFileInWorldStorage(fileName, type))
			{
				return MyAPIGateway.Utilities.SerializeFromXML<T>(textReader.ReadToEnd());
			}
		}

		public static string ReadFileFromWorldStorage(string fileName, Type type)
		{
			if (!MyAPIGateway.Utilities.FileExistsInWorldStorage(fileName, type))
				return string.Empty;

			using (TextReader textReader = MyAPIGateway.Utilities.ReadFileInWorldStorage(fileName, type))
			{
				return textReader.ReadToEnd();
			}
		}
	}
}