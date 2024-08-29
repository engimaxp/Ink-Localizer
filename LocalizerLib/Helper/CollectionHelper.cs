namespace InkLocalizer.Helper;

internal static class CollectionHelper {
	public static void AddRange<TKey, TValue>(this IDictionary<TKey, TValue> dic, IDictionary<TKey, TValue> dicToAdd) {
		dicToAdd.ForEach(x => dic.Add(x.Key, x.Value));
	}

	private static void ForEach<T>(this IEnumerable<T> source, Action<T> action) {
		foreach (T item in source)
			action(item);
	}
}