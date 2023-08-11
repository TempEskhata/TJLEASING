//-----------------------------------------------------------------------
// <copyright file="HttpSenderEmulator.cs" company="Credit Bank of Moscow">
//     Copyright (c) Credit Bank of Moscow. All rights reserved.
// </copyright>
// <summary>
//     Реализует эмулятор http-запросов для шлюза TJGosstandart.
// </summary>
//-----------------------------------------------------------------------

namespace Gateways.TJGosstandart.Emulator
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using DPS.Common.Tools;

	/// <summary>
	/// Реализует эмулятор для шлюза.
	/// </summary>
	public class HttpSenderEmulator : Gateways.HttpSenderEmulator
	{
		/// <summary>
		/// Возвращает путь к файлу ответа для заданных URL и данных POST запроса.
		/// </summary>
		/// <param name="uri">Url, для которогго надо эмулировать ответ</param>
		/// <param name="data">Данные для POST запроса</param>
		/// <returns>Путь к файлу ответа</returns>
		public override string GetResponseFilePath(Uri uri, byte[] data)
		{
			string responseFilesFolder = nameof(TJGosstandartGateway);
			string responseFilePath = responseFilesFolder;

			var requestParams = new StringList(uri.Query.TrimStart(new char[] { '?' }), "&");

			string fileName;
			responseFilePath = this.OperationResponseFileMap.TryGetValue(requestParams["type"], out fileName) ? fileName : responseFilePath;

			return responseFilePath;
		}
	}
}
