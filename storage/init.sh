#!/bin/sh
# Prepara o storage (S3): buckets, acesso público por prefixo e o usuário da API.
# Idempotente: pode rodar a cada "up" sem duplicar nada.
set -e

# 1. Atalho "local" apontando pro storage, com o usuário admin
mc alias set local http://storage:9000 "$MINIO_ROOT_USER" "$MINIO_ROOT_PASSWORD"

# 2. Buckets
mc mb --ignore-existing "local/$STORAGE_BUCKET_PUBLIC"
mc mb --ignore-existing "local/$STORAGE_BUCKET_PRIVATE"

# 3. Leitura anônima só nos prefixos públicos. O resto do bucket continua privado.
#    Novo prefixo público (ex.: suppliers/) = mais uma linha aqui
mc anonymous set download "local/$STORAGE_BUCKET_PUBLIC/products"

# 4. Usuário da API com acesso só aos dois buckets (a API não usa o admin)
cat > /tmp/nextflow-api-policy.json <<POLICY
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": ["s3:ListBucket"],
      "Resource": ["arn:aws:s3:::$STORAGE_BUCKET_PUBLIC", "arn:aws:s3:::$STORAGE_BUCKET_PRIVATE"]
    },
    {
      "Effect": "Allow",
      "Action": ["s3:GetObject", "s3:PutObject", "s3:DeleteObject"],
      "Resource": ["arn:aws:s3:::$STORAGE_BUCKET_PUBLIC/*", "arn:aws:s3:::$STORAGE_BUCKET_PRIVATE/*"]
    }
  ]
}
POLICY

mc admin policy create local nextflow-api /tmp/nextflow-api-policy.json
mc admin user add local "$STORAGE_ACCESS_KEY" "$STORAGE_SECRET_KEY"

# Ligar de novo uma política já ligada não dá erro: pode rodar sempre
mc admin policy attach local nextflow-api --user "$STORAGE_ACCESS_KEY"

echo "Storage pronto."
