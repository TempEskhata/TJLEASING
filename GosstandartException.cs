//-----------------------------------------------------------------------
// <copyright file="GosstandartException.cs" company="">
//     Copyright (c). All rights reserved.
// </copyright>
// <summary>
//     Исключение шлюза TJGosstandart.
// </summary>
//-----------------------------------------------------------------------

namespace Gateways.TJGosstandart
{
    using System;
    using System.Runtime.InteropServices;
    using System.Runtime.Serialization;

    /// <summary>
    /// Класс базового исключения при работе с сервисом Bus.
    /// </summary>
    [Serializable]
    [ComVisible(true)]
    internal class GosstandartException : Exception
    {
        /// <summary>
        /// Инициализирует новый экземпляр класса <see cref="GosstandartException" />
        /// </summary>
        public GosstandartException()
            : base()
        {
        }

        /// <summary>
        /// Инициализирует новый экземпляр класса <see cref="GosstandartException" /> используя указанное сообщение об ошибке
        /// </summary>
        /// <param name="message">Сообщение об ошибке</param>
        public GosstandartException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Инициализирует новый экземпляр класса <see cref="GosstandartException" />
        /// используя указанные сообщение об ошибке и внутреннее исключение
        /// </summary>
        /// <param name="message">Сообщение об ошибке</param>
        /// <param name="innerException">Внутреннее исключение</param>
        public GosstandartException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        /// <summary>
        /// Инициализирует новый экземпляр класса <see cref="GosstandartException" /> используя сериализованные данные
        /// </summary>
        /// <param name="info">Объект, содержащий сериализованные данные о созданном исключении</param>
        /// <param name="context">Контекстная информация об источнике</param>
        protected GosstandartException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}