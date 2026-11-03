"""Local fallback for radius-limited nearest-neighbour queries used by the art preview."""
import itertools
import numpy as np

class cKDTree:
    def __init__(self, points):
        self.data = np.asarray(points)
        self.n = len(self.data)
        self._grid = None
        self._size = None

    def query(self, origins, k, distance_upper_bound):
        size = float(distance_upper_bound)
        if self._grid is None or self._size != size:
            self._size = size
            self._grid = {}
            cells = np.floor(self.data / size).astype(int)
            for i, cell in enumerate(cells):
                self._grid.setdefault(tuple(cell), []).append(i)
        distances = np.full((len(origins), k), np.inf)
        indices = np.full((len(origins), k), self.n, dtype=int)
        for j, origin in enumerate(origins):
            base = np.floor(origin / size).astype(int)
            candidates = []
            for off in itertools.product((-1, 0, 1), repeat=3):
                candidates.extend(self._grid.get(tuple(base + off), ()))
            if not candidates:
                continue
            ids = np.asarray(candidates, dtype=int)
            ds = np.linalg.norm(self.data[ids] - origin, axis=1)
            chosen = np.flatnonzero(ds < size)
            if len(chosen) > k:
                chosen = chosen[np.argpartition(ds[chosen], k - 1)[:k]]
            chosen = chosen[np.argsort(ds[chosen])]
            distances[j, :len(chosen)] = ds[chosen]
            indices[j, :len(chosen)] = ids[chosen]
        return distances, indices

