import React, { useState } from 'react';
import './App.css';

function App() {
  const [nombre, setNombre] = useState('');
  const [resultats, setResultats] = useState({});

  const handleSubmit = async (e) => {
    e.preventDefault();

    const num = parseInt(nombre, 10);
    if (isNaN(num)) {
      setResultats({ error: 'Erreur : Entrez un nombre entier valide' });
      return;
    }
    setResultats((prev) => ({ ...prev }));

    try {
      const response = await fetch('http://localhost:5001/calculate', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({ nombre: num }),
      });

      if (!response.ok) {
        throw new Error(`Erreur HTTP : ${response.status}`);
      }

      const data = await response.json();

      if (data.exists !== undefined) {
        data.insertionStatus = data.exists ? 'Les données existaient déjà dans la base.' : 'Les données ont été insérées dans la base.';
      }

      setResultats(data);

    } catch (error) {
      console.error(error);
      setResultats({ error: 'Erreur : Impossible de communiquer avec le serveur' });
    }
  };

  return (
    <div className="App">
      <h1>Analyse d'un Nombre</h1>
      <form onSubmit={handleSubmit}>
        <input
          type="number"
          value={nombre}
          onChange={(e) => setNombre(e.target.value)}
          placeholder="Entrez un nombre entier"
        />
        <button type="submit">
          Calculer
        </button>
      </form>

      {resultats && Object.keys(resultats).length > 0 && (
        <div className="resultats">
          {resultats.error ? (
            <p style={{ color: 'red' }}>{resultats.error}</p>
          ) : (
            <div>
              {resultats.mysqlResult && resultats.syracuseResult ? (
                <>
                  <p><strong>Pair :</strong> {resultats.mysqlResult?.data.estPair ? 'Oui' : 'Non'}</p>
                  <p><strong>Parfait :</strong> {resultats.mysqlResult?.data.estParfait ? 'Oui' : 'Non'}</p>
                  <p><strong>Premier :</strong> {resultats.mysqlResult?.data.estPremier ? 'Oui' : 'Non'}</p>

                  <p><strong>Suite de Syracuse :</strong></p>
                  <pre>{JSON.stringify(resultats.syracuseResult?.data.suiteSyracuse, null, 2)}</pre>

                  <p><strong>Insertion des données</strong></p>
                </>
              ) : (
                <>
                  <p><strong>Pair :</strong> {resultats.estPair ? 'Oui' : 'Non'}</p>
                  <p><strong>Parfait :</strong> {resultats.estParfait ? 'Oui' : 'Non'}</p>
                  <p><strong>Premier :</strong> {resultats.estPremier ? 'Oui' : 'Non'}</p>

                  <p><strong>Suite de Syracuse :</strong></p>
                  <pre>{JSON.stringify(resultats.syracuse, null, 2)}</pre>

                  <p><strong>Données existantes en base</strong></p>
                </>
              )}
            </div>
          )}
        </div>
      )}
    </div>
  );
}

export default App;