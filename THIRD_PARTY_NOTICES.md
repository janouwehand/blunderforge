# Third-party notices

## Stockfish

BlunderForge's container image includes Stockfish 17.1 as a separate executable.

- Project: https://stockfishchess.org/
- Source repository: https://github.com/official-stockfish/Stockfish
- Exact source revision: `03e27488f3d21d8ff4dbf3065603afa21dbd0ef3`
- Corresponding source archive: https://github.com/official-stockfish/Stockfish/archive/03e27488f3d21d8ff4dbf3065603afa21dbd0ef3.tar.gz
- License: GNU General Public License version 3 or later

The production image also contains the GPL license and the complete corresponding
source used to build its Stockfish binary under `/usr/share/stockfish/`.

BlunderForge communicates with Stockfish through its UCI process protocol. The
BlunderForge source code remains available under the MIT license; Stockfish
remains available under its own GPL terms.
