using System;

namespace Sisus.Pool
{
	/// <summary>
	/// Attribute that can be added to <see cref="PoolableBehaviour{TPoolableBehaviour}"/>
	/// classes to configure the settings for their object pool.
	/// </summary>
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
	public class ObjectPoolSettingsAttribute : Attribute
	{
		/// <summary>
		/// Initial number of instances to instantiate into object pools,
		/// if not otherwise specified using <see cref="InitialSize"/>.
		/// </summary>
		internal const int DEFAULT_INITIAL_SIZE = 32;

		/// <summary>
		/// Maximum number of instances that can be stored in object pools,
		/// if not otherwise specified using <see cref="InitialSize"/>.
		/// </summary>
		internal const int DEFAULT_MAX_SIZE = 128;

		/// <summary>
		/// Initial number of instances to instantiate into the object pool.
		/// </summary>
		public int InitialSize { get; } = DEFAULT_INITIAL_SIZE;

		/// <summary>
		/// Maximum number of instances that can be stored in the object pool.
		/// </summary>
		public int MaxSize { get; } = DEFAULT_MAX_SIZE;
	}
}
