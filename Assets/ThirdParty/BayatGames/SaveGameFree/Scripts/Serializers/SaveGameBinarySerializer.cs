using System;
using System.IO;
using System.Runtime.Serialization;
using System.Text;
using System.Xml;
using UnityEngine;

namespace BayatGames.SaveGameFree.Serializers
{

	/// <summary>
	/// Saves data as binary XML using the declared type's data contract.
	/// </summary>
	public class SaveGameBinarySerializer : ISaveGameSerializer
	{

		/// <summary>
		/// Serialize the specified object to a binary stream.
		/// </summary>
		public void Serialize<T> ( T obj, Stream stream, Encoding encoding )
		{
			try
			{
				using ( XmlDictionaryWriter writer = XmlDictionaryWriter.CreateBinaryWriter ( stream, null, null, false ) )
				{
					CreateSerializer<T> ().WriteObject ( writer, obj );
				}
			}
			catch ( Exception ex )
			{
				Debug.LogException ( ex );
			}
		}

		/// <summary>
		/// Deserialize the specified object from a binary stream.
		/// </summary>
		public T Deserialize<T> ( Stream stream, Encoding encoding )
		{
			try
			{
				using ( MemoryStream buffer = new MemoryStream () )
				{
					// The binary reader owns its input. Keep the caller's stream open.
					stream.CopyTo ( buffer );
					buffer.Position = 0;
					using ( XmlDictionaryReader reader = XmlDictionaryReader.CreateBinaryReader ( buffer, XmlDictionaryReaderQuotas.Max ) )
					{
						return ( T )CreateSerializer<T> ().ReadObject ( reader );
					}
				}
			}
			catch ( Exception ex )
			{
				Debug.LogException ( ex );
			}
			return default(T);
		}

		private static DataContractSerializer CreateSerializer<T> ()
		{
			return new DataContractSerializer ( typeof ( T ), new DataContractSerializerSettings
			{
				PreserveObjectReferences = true
			} );
		}

	}

}
